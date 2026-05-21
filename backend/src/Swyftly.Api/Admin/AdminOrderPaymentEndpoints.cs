using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Application.Orders;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Admin;

public static class AdminOrderPaymentEndpoints
{
    private const int DefaultTake = 100;
    private const int MaxTake = 250;

    public static IEndpointRouteBuilder MapAdminOrderPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/admin/orders")
            .WithTags("Admin Orders")
            .RequireAuthorization(SwyftlyPolicies.FinanceRead);

        orders.MapGet("", GetOrdersAsync)
            .WithName("GetAdminOrders")
            .WithSummary("Returns recent marketplace orders for finance/admin read workflows.")
            .Produces<IReadOnlyCollection<AdminOrderSummaryResponse>>(StatusCodes.Status200OK);

        orders.MapGet("/{orderId:guid}", GetOrderAsync)
            .WithName("GetAdminOrder")
            .WithSummary("Returns one marketplace order for finance/admin read workflows.")
            .Produces<AdminOrderDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var payments = app.MapGroup("/api/admin/payments")
            .WithTags("Admin Payments")
            .RequireAuthorization(SwyftlyPolicies.FinanceRead);

        payments.MapGet("", GetPaymentsAsync)
            .WithName("GetAdminPayments")
            .WithSummary("Returns recent payment records for finance/admin read workflows.")
            .Produces<IReadOnlyCollection<AdminPaymentSummaryResponse>>(StatusCodes.Status200OK);

        payments.MapGet("/reconciliation-candidates", GetPaymentReconciliationCandidatesAsync)
            .WithName("GetAdminPaymentReconciliationCandidates")
            .WithSummary("Returns read-only payment records that need manual provider reconciliation.")
            .Produces<IReadOnlyCollection<AdminPaymentReconciliationCandidateResponse>>(StatusCodes.Status200OK);

        payments.MapGet("/{paymentId:guid}", GetPaymentAsync)
            .WithName("GetAdminPayment")
            .WithSummary("Returns one payment record for finance/admin read workflows.")
            .Produces<AdminPaymentDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetOrdersAsync(
        string? status,
        int? take,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.Shipments)
            .AsNoTracking();

        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(order => order.Status == parsedStatus);
        }

        var orders = await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(ClampTake(take))
            .ToListAsync(cancellationToken);

        var sellerNames = await SellerNamesAsync(dbContext, orders.Select(order => order.SellerId), cancellationToken);
        var paymentStatuses = await LatestPaymentStatusesAsync(dbContext, orders.Select(order => order.Id), cancellationToken);

        return HttpResults.Ok(orders
            .Select(order => MapOrderSummary(order, sellerNames, paymentStatuses))
            .ToArray());
    }

    private static async Task<IResult> GetOrderAsync(
        Guid orderId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var order = await OrderDetailQuery(dbContext)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return NotFound("AdminOrders.NotFound", "Order was not found.");
        }

        var sellerNames = await SellerNamesAsync(dbContext, [order.SellerId], cancellationToken);
        var payments = await dbContext.Payments
            .Where(payment => payment.OrderId == order.Id)
            .AsNoTracking()
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .Select(payment => MapPaymentSummary(payment))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(MapOrderDetail(order, sellerNames, payments));
    }

    private static async Task<IResult> GetPaymentsAsync(
        string? status,
        Guid? orderId,
        int? take,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Payments.AsNoTracking();

        if (Enum.TryParse<PaymentStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(payment => payment.Status == parsedStatus);
        }

        if (orderId.HasValue)
        {
            query = query.Where(payment => payment.OrderId == orderId.Value);
        }

        var payments = await query
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .Take(ClampTake(take))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(payments.Select(MapPaymentSummary).ToArray());
    }

    private static async Task<IResult> GetPaymentReconciliationCandidatesAsync(
        int? olderThanMinutes,
        int? take,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(ClampOlderThanMinutes(olderThanMinutes)));

        var failedEventPaymentIds = dbContext.PaymentEvents
            .Where(paymentEvent => paymentEvent.PaymentId.HasValue
                && paymentEvent.ProcessingStatus == PaymentEventProcessingStatus.Failed)
            .Select(paymentEvent => paymentEvent.PaymentId!.Value);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                failedEventPaymentIds.Contains(payment.Id)
                || ((payment.Status == PaymentStatus.Pending || payment.Status == PaymentStatus.Authorized)
                    && payment.CreatedAtUtc <= cutoff))
            .OrderBy(payment => payment.CreatedAtUtc)
            .Take(ClampTake(take))
            .ToListAsync(cancellationToken);

        var paymentIds = payments.Select(payment => payment.Id).ToArray();
        var latestEvents = await dbContext.PaymentEvents
            .Where(paymentEvent => paymentEvent.PaymentId.HasValue && paymentIds.Contains(paymentEvent.PaymentId.Value))
            .AsNoTracking()
            .OrderByDescending(paymentEvent => paymentEvent.ReceivedAtUtc)
            .ToListAsync(cancellationToken);

        var latestEventByPaymentId = latestEvents
            .GroupBy(paymentEvent => paymentEvent.PaymentId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var failedEventPaymentIdSet = latestEvents
            .Where(paymentEvent => paymentEvent.ProcessingStatus == PaymentEventProcessingStatus.Failed)
            .Select(paymentEvent => paymentEvent.PaymentId!.Value)
            .ToHashSet();

        return HttpResults.Ok(payments
            .Select(payment => MapReconciliationCandidate(
                payment,
                latestEventByPaymentId.GetValueOrDefault(payment.Id),
                failedEventPaymentIdSet.Contains(payment.Id)))
            .ToArray());
    }

    private static async Task<IResult> GetPaymentAsync(
        Guid paymentId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            return NotFound("AdminPayments.NotFound", "Payment was not found.");
        }

        var order = await dbContext.Orders
            .Include(item => item.Items)
            .AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == payment.OrderId, cancellationToken);
        var events = await dbContext.PaymentEvents
            .Where(paymentEvent => paymentEvent.PaymentId == payment.Id)
            .AsNoTracking()
            .OrderByDescending(paymentEvent => paymentEvent.ReceivedAtUtc)
            .Select(paymentEvent => new AdminPaymentEventResponse(
                paymentEvent.Id,
                paymentEvent.Provider,
                paymentEvent.ProviderEventId,
                paymentEvent.EventType,
                paymentEvent.ProcessingStatus.ToString(),
                paymentEvent.ReceivedAtUtc,
                paymentEvent.ProcessedAtUtc,
                paymentEvent.ErrorMessage))
            .ToListAsync(cancellationToken);

        AdminPaymentOrderResponse? orderResponse = null;
        if (order is not null)
        {
            orderResponse = new AdminPaymentOrderResponse(
                order.Id,
                order.BuyerId,
                order.SellerId,
                order.Status.ToString(),
                order.Items.Count,
                order.TotalAmount,
                order.CreatedAtUtc);
        }

        return HttpResults.Ok(new AdminPaymentDetailResponse(
            payment.Id,
            payment.OrderId,
            payment.BuyerId,
            payment.Provider,
            payment.ProviderReference,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.PaidAtUtc,
            payment.FailedAtUtc,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            orderResponse,
            events));
    }

    private static IQueryable<Order> OrderDetailQuery(SwyftlyDbContext dbContext) =>
        dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .AsNoTracking();

    private static async Task<Dictionary<Guid, string?>> SellerNamesAsync(
        SwyftlyDbContext dbContext,
        IEnumerable<Guid> sellerIds,
        CancellationToken cancellationToken)
    {
        var ids = sellerIds.Distinct().ToArray();
        return await dbContext.SellerProfiles
            .Where(seller => ids.Contains(seller.Id))
            .AsNoTracking()
            .ToDictionaryAsync(
                seller => seller.Id,
                seller => seller.DisplayName ?? seller.BusinessName,
                cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> LatestPaymentStatusesAsync(
        SwyftlyDbContext dbContext,
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        var ids = orderIds.Distinct().ToArray();
        var payments = await dbContext.Payments
            .Where(payment => ids.Contains(payment.OrderId))
            .AsNoTracking()
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .Select(payment => new { payment.OrderId, Status = payment.Status.ToString() })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(payment => payment.OrderId)
            .ToDictionary(group => group.Key, group => group.First().Status);
    }

    private static AdminOrderSummaryResponse MapOrderSummary(
        Order order,
        IReadOnlyDictionary<Guid, string?> sellerNames,
        IReadOnlyDictionary<Guid, string> paymentStatuses) =>
        new(
            order.Id,
            order.BuyerId,
            order.SellerId,
            sellerNames.GetValueOrDefault(order.SellerId),
            order.Status.ToString(),
            order.Items.Count,
            order.ItemsSubtotal,
            order.ShippingAmount,
            order.PlatformFeeAmount,
            order.DiscountAmount,
            order.TotalAmount,
            paymentStatuses.GetValueOrDefault(order.Id),
            order.Shipments
                .OrderByDescending(shipment => shipment.UpdatedAtUtc)
                .Select(shipment => shipment.Status.ToString())
                .FirstOrDefault(),
            order.CreatedAtUtc,
            order.UpdatedAtUtc);

    private static AdminOrderDetailResponse MapOrderDetail(
        Order order,
        IReadOnlyDictionary<Guid, string?> sellerNames,
        IReadOnlyCollection<AdminPaymentSummaryResponse> payments) =>
        new(
            order.Id,
            order.BuyerId,
            order.SellerId,
            sellerNames.GetValueOrDefault(order.SellerId),
            order.CartId,
            order.Status.ToString(),
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResult(
                    item.Id,
                    item.ProductId,
                    item.ProductVariantId,
                    item.ProductTitle,
                    item.Sku,
                    item.Size,
                    item.Colour,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal))
                .ToArray(),
            order.ItemsSubtotal,
            order.ShippingAmount,
            order.PlatformFeeAmount,
            order.DiscountAmount,
            order.TotalAmount,
            order.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .Select(history => new OrderStatusHistoryResult(
                    history.Id,
                    history.PreviousStatus?.ToString(),
                    history.NewStatus.ToString(),
                    history.ChangedAtUtc,
                    history.Reason))
                .ToArray(),
            order.Shipments
                .OrderBy(shipment => shipment.CreatedAtUtc)
                .Select(shipment => new ShipmentResult(
                    shipment.Id,
                    shipment.Status.ToString(),
                    shipment.CarrierName,
                    shipment.TrackingNumber,
                    shipment.TrackingUrl,
                    shipment.ShippedAtUtc,
                    shipment.DeliveredAtUtc,
                    shipment.Events
                        .OrderBy(shipmentEvent => shipmentEvent.OccurredAtUtc)
                        .Select(shipmentEvent => new ShipmentEventResult(
                            shipmentEvent.Id,
                            shipmentEvent.Status.ToString(),
                            shipmentEvent.EventType,
                            shipmentEvent.Message,
                            shipmentEvent.CarrierName,
                            shipmentEvent.TrackingNumber,
                            shipmentEvent.OccurredAtUtc))
                        .ToArray()))
                .ToArray(),
            payments,
            order.CreatedAtUtc,
            order.UpdatedAtUtc);

    private static AdminPaymentSummaryResponse MapPaymentSummary(Payment payment) =>
        new(
            payment.Id,
            payment.OrderId,
            payment.BuyerId,
            payment.Provider,
            payment.ProviderReference,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.PaidAtUtc,
            payment.FailedAtUtc,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc);

    private static AdminPaymentReconciliationCandidateResponse MapReconciliationCandidate(
        Payment payment,
        PaymentEvent? latestEvent,
        bool hasFailedEvent)
    {
        var reasonCode = hasFailedEvent
            ? "FailedWebhookEvent"
            : payment.Status == PaymentStatus.Authorized
                ? "StaleAuthorizedPayment"
                : "StalePendingPayment";
        var recommendedAction = hasFailedEvent
            ? "Review the provider dashboard and webhook error before retrying or manually resolving the payment."
            : "Check the provider dashboard for an abandoned, completed, or failed checkout before taking manual action.";

        return new AdminPaymentReconciliationCandidateResponse(
            payment.Id,
            payment.OrderId,
            payment.BuyerId,
            payment.Provider,
            payment.ProviderReference,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            reasonCode,
            recommendedAction,
            latestEvent is null
                ? null
                : new AdminPaymentEventResponse(
                    latestEvent.Id,
                    latestEvent.Provider,
                    latestEvent.ProviderEventId,
                    latestEvent.EventType,
                    latestEvent.ProcessingStatus.ToString(),
                    latestEvent.ReceivedAtUtc,
                    latestEvent.ProcessedAtUtc,
                    latestEvent.ErrorMessage));
    }

    private static int ClampTake(int? take) =>
        Math.Clamp(take.GetValueOrDefault(DefaultTake), 1, MaxTake);

    private static int ClampOlderThanMinutes(int? olderThanMinutes) =>
        Math.Clamp(olderThanMinutes.GetValueOrDefault(30), 5, 1440);

    private static IResult NotFound(string title, string detail) =>
        HttpResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);
}

public sealed record AdminOrderSummaryResponse(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string? SellerDisplayName,
    string Status,
    int ItemCount,
    decimal ItemsSubtotal,
    decimal ShippingAmount,
    decimal PlatformFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? PaymentStatus,
    string? ShipmentStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminOrderDetailResponse(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string? SellerDisplayName,
    Guid CartId,
    string Status,
    IReadOnlyCollection<OrderItemResult> Items,
    decimal ItemsSubtotal,
    decimal ShippingAmount,
    decimal PlatformFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    IReadOnlyCollection<OrderStatusHistoryResult> StatusHistory,
    IReadOnlyCollection<ShipmentResult> Shipments,
    IReadOnlyCollection<AdminPaymentSummaryResponse> Payments,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminPaymentSummaryResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid BuyerId,
    string Provider,
    string? ProviderReference,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? FailedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminPaymentDetailResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid BuyerId,
    string Provider,
    string? ProviderReference,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? FailedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    AdminPaymentOrderResponse? Order,
    IReadOnlyCollection<AdminPaymentEventResponse> Events);

public sealed record AdminPaymentReconciliationCandidateResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid BuyerId,
    string Provider,
    string? ProviderReference,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string ReasonCode,
    string RecommendedAction,
    AdminPaymentEventResponse? LatestEvent);

public sealed record AdminPaymentOrderResponse(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string Status,
    int ItemCount,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminPaymentEventResponse(
    Guid PaymentEventId,
    string Provider,
    string ProviderEventId,
    string EventType,
    string ProcessingStatus,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProcessedAtUtc,
    string? ErrorMessage);
