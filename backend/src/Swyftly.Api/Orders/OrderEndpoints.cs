using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Application.Identity;
using Swyftly.Application.Orders;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Orders;

public static class OrderEndpoints
{
    private static readonly TimeSpan DefaultReservationDuration = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapPost("/from-cart", CreateFromCartAsync)
            .WithName("CreateOrderFromCart")
            .WithSummary("Creates a pending-payment order from the authenticated buyer's active cart.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapGet("", GetBuyerOrdersAsync)
            .WithName("GetBuyerOrders")
            .WithSummary("Returns orders owned by the authenticated buyer.")
            .Produces<IReadOnlyCollection<OrderResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/{orderId:guid}", GetBuyerOrderAsync)
            .WithName("GetBuyerOrder")
            .WithSummary("Returns one order owned by the authenticated buyer.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var buyerAliasGroup = app.MapGroup("/api/buyer/orders")
            .WithTags("Buyer Orders")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerAliasGroup.MapGet("", GetBuyerOrdersAsync)
            .WithSummary("Returns orders owned by the authenticated buyer.")
            .Produces<IReadOnlyCollection<OrderResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerAliasGroup.MapGet("/{orderId:guid}", GetBuyerOrderAsync)
            .WithSummary("Returns one order owned by the authenticated buyer.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var sellerGroup = app.MapGroup("/api/seller/orders")
            .WithTags("Seller Orders")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        sellerGroup.MapGet("", GetSellerOrdersAsync)
            .WithName("GetSellerOrders")
            .WithSummary("Returns orders containing products from the authenticated seller.")
            .Produces<IReadOnlyCollection<OrderResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("/{orderId:guid}", GetSellerOrderAsync)
            .WithName("GetSellerOrder")
            .WithSummary("Returns one order containing products from the authenticated seller.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{orderId:guid}/mark-processing", MarkProcessingAsync)
            .WithName("MarkSellerOrderProcessing")
            .WithSummary("Marks a paid seller order as processing and creates the initial shipment record.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/{orderId:guid}/tracking", AddTrackingAsync)
            .WithName("AddSellerOrderTracking")
            .WithSummary("Adds or replaces manual tracking details for a seller order.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/{orderId:guid}/mark-shipped", MarkShippedAsync)
            .WithName("MarkSellerOrderShipped")
            .WithSummary("Marks a seller order as shipped and moves the shipment in transit.")
            .Produces<OrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateFromCartAsync(
        CreateOrderFromCartApiRequest request,
        ClaimsPrincipal principal,
        IOrderCreationService orderCreationService,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var result = await orderCreationService.CreateFromCartAsync(
            new CreateOrderFromCartRequest(
                buyer.Id,
                request.CartId,
                timeProvider.GetUtcNow(),
                request.ReservationMinutes is > 0
                    ? TimeSpan.FromMinutes(request.ReservationMinutes.Value)
                    : DefaultReservationDuration),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetBuyerOrdersAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var orders = await OrderQuery(dbContext)
            .Where(order => order.BuyerId == buyer.Id)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(orders.Select(Map).ToArray());
    }

    private static async Task<IResult> GetBuyerOrderAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var order = await OrderQuery(dbContext)
            .SingleOrDefaultAsync(
                order => order.Id == orderId && order.BuyerId == buyer.Id,
                cancellationToken);

        return order is null
            ? OrderNotFound()
            : HttpResults.Ok(Map(order));
    }

    private static async Task<IResult> GetSellerOrdersAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var orders = await OrderQuery(dbContext)
            .Where(order => order.SellerId == seller.Id)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(orders.Select(Map).ToArray());
    }

    private static async Task<IResult> GetSellerOrderAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var order = await OrderQuery(dbContext)
            .SingleOrDefaultAsync(
                order => order.Id == orderId && order.SellerId == seller.Id,
                cancellationToken);

        return order is null
            ? OrderNotFound()
            : HttpResults.Ok(Map(order));
    }

    private static async Task<IResult> MarkProcessingAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IOrderFulfillmentService fulfillmentService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var result = await fulfillmentService.MarkProcessingAsync(
            new OrderFulfillmentRequest(seller.Id, orderId, timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> AddTrackingAsync(
        Guid orderId,
        AddOrderTrackingApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IOrderFulfillmentService fulfillmentService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var result = await fulfillmentService.AddTrackingAsync(
            new AddOrderTrackingRequest(
                seller.Id,
                orderId,
                request.CarrierName,
                request.TrackingNumber,
                request.TrackingUrl,
                request.Note,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> MarkShippedAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IOrderFulfillmentService fulfillmentService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var result = await fulfillmentService.MarkShippedAsync(
            new OrderFulfillmentRequest(seller.Id, orderId, timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static IQueryable<Order> OrderQuery(SwyftlyDbContext dbContext) =>
        dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .AsNoTracking();

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static OrderResult Map(Order order) =>
        new(
            order.Id,
            order.BuyerId,
            order.SellerId,
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
                .ToArray());

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Orders.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "Orders.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult OrderNotFound() =>
        HttpResults.Problem(
            title: "Orders.NotFound",
            detail: "Order was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record CreateOrderFromCartApiRequest(
    Guid? CartId,
    int? ReservationMinutes);

public sealed record AddOrderTrackingApiRequest(
    string CarrierName,
    string TrackingNumber,
    string? TrackingUrl,
    string? Note);
