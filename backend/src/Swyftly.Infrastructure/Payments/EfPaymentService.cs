using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swyftly.Application.Advertising;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Ledger;
using Swyftly.Application.Payments;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Payments;

public sealed class EfPaymentService(
    SwyftlyDbContext dbContext,
    IPaymentProvider paymentProvider,
    ILedgerService ledgerService,
    IAdTrackingService adTrackingService,
    IOptions<PaymentProviderOptions> paymentOptions,
    TimeProvider timeProvider) : IPaymentService
{
    private readonly PaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public async Task<Result<PaymentInitiationResponse>> InitiatePaymentAsync(
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BuyerId == Guid.Empty || request.OrderId == Guid.Empty)
        {
            return Result<PaymentInitiationResponse>.Failure(Error.Validation([
                new ValidationFailure("payment", "Buyer id and order id are required.")
            ]));
        }

        var order = await dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order => order.Id == request.OrderId && order.BuyerId == request.BuyerId,
                cancellationToken);
        if (order is null)
        {
            return Result<PaymentInitiationResponse>.Failure(
                Error.NotFound("Payments.OrderNotFound", "Order was not found."));
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return Result<PaymentInitiationResponse>.Failure(
                Error.Conflict("Payments.OrderNotPendingPayment", "Only pending-payment orders can start payment."));
        }

        var now = timeProvider.GetUtcNow();
        var payment = new Payment(
            order.Id,
            order.BuyerId,
            paymentProvider.ProviderName,
            order.TotalAmount,
            _paymentOptions.DefaultCurrency,
            now);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var providerResult = await paymentProvider.InitializePaymentAsync(
            new PaymentInitiationRequest(
                order.Id,
                order.BuyerId,
                order.TotalAmount,
                _paymentOptions.DefaultCurrency,
                $"Swyftly order {order.Id}",
                new Uri(_paymentOptions.SuccessRedirectUrl),
                new Uri(_paymentOptions.FailureRedirectUrl),
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id.ToString(),
                    ["paymentId"] = payment.Id.ToString()
                }),
            cancellationToken);

        if (providerResult.IsFailure)
        {
            payment.MarkFailed(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<PaymentInitiationResponse>.Failure(providerResult.Error);
        }

        payment.SetProviderReference(providerResult.Value.ProviderReference, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<PaymentInitiationResponse>.Success(Map(payment, providerResult.Value.CheckoutUrl));
    }

    public async Task<Result<PaymentWebhookProcessingResult>> ProcessWebhookAsync(
        ProcessPaymentWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Provider, paymentProvider.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PaymentWebhookProcessingResult>.Failure(
                Error.NotFound("Payments.ProviderNotFound", "Payment provider was not found."));
        }

        var signatureResult = await paymentProvider.VerifyWebhookSignatureAsync(
            new PaymentWebhookSignatureVerificationRequest(request.Payload, request.Headers),
            cancellationToken);
        if (signatureResult.IsFailure)
        {
            return Result<PaymentWebhookProcessingResult>.Failure(signatureResult.Error);
        }

        var parsedResult = await paymentProvider.ParseWebhookAsync(
            new PaymentWebhookParseRequest(request.Payload, request.Headers),
            cancellationToken);
        if (parsedResult.IsFailure)
        {
            return Result<PaymentWebhookProcessingResult>.Failure(parsedResult.Error);
        }

        var parsedEvent = parsedResult.Value;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var duplicateEvent = await dbContext.PaymentEvents.SingleOrDefaultAsync(
            paymentEvent => paymentEvent.Provider == parsedEvent.Provider
                && paymentEvent.ProviderEventId == parsedEvent.EventId,
            cancellationToken);
        if (duplicateEvent is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<PaymentWebhookProcessingResult>.Success(new PaymentWebhookProcessingResult(
                duplicateEvent.Id,
                duplicateEvent.PaymentId,
                duplicateEvent.ProviderEventId,
                duplicateEvent.ProcessingStatus.ToString(),
                "Unchanged",
                null));
        }

        var now = timeProvider.GetUtcNow();
        var payment = await dbContext.Payments.SingleOrDefaultAsync(
            payment => payment.Provider == parsedEvent.Provider
                && payment.ProviderReference == parsedEvent.ProviderReference,
            cancellationToken);
        var paymentEvent = new PaymentEvent(
            payment?.Id,
            parsedEvent.Provider,
            parsedEvent.EventId,
            parsedEvent.EventType,
            parsedEvent.Payload,
            now);
        dbContext.PaymentEvents.Add(paymentEvent);

        if (payment is null)
        {
            paymentEvent.MarkFailed("Payment was not found for provider reference.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PaymentWebhookProcessingResult>.Failure(
                Error.NotFound("Payments.PaymentNotFound", "Payment was not found for provider reference."));
        }

        var order = await dbContext.Orders
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == payment.OrderId, cancellationToken);

        if (IsPaidStatus(parsedEvent.Status))
        {
            await ProcessSuccessfulPaymentAsync(payment, order, now, cancellationToken);
        }
        else if (IsFailedStatus(parsedEvent.Status))
        {
            await ProcessFailedPaymentAsync(payment, order, now, cancellationToken);
        }

        paymentEvent.MarkProcessed(payment.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<PaymentWebhookProcessingResult>.Success(new PaymentWebhookProcessingResult(
            paymentEvent.Id,
            payment.Id,
            paymentEvent.ProviderEventId,
            paymentEvent.ProcessingStatus.ToString(),
            payment.Status.ToString(),
            order.Status.ToString()));
    }

    private async Task ProcessSuccessfulPaymentAsync(
        Payment payment,
        Order order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        payment.MarkPaid(now);
        order.ChangeStatus(OrderStatus.Paid, now, "PaymentConfirmed");
        TrackLatestOrderStatusHistory(order);

        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == order.CartId && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            reservation.Confirm(now);
        }

        var ledgerResult = await ledgerService.CreateSuccessfulPaymentEntriesAsync(
            new SuccessfulPaymentLedgerRequest(
                payment.Id,
                order.Id,
                order.BuyerId,
                order.SellerId,
                payment.Amount,
                payment.Currency,
                now),
            cancellationToken);
        if (ledgerResult.IsFailure)
        {
            throw new InvalidOperationException(ledgerResult.Error.Description);
        }

        await adTrackingService.AttributeOrderConversionsAsync(order.Id, cancellationToken);
    }

    private async Task ProcessFailedPaymentAsync(
        Payment payment,
        Order order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        payment.MarkFailed(now);
        order.ChangeStatus(OrderStatus.Cancelled, now, "PaymentFailed");
        TrackLatestOrderStatusHistory(order);

        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == order.CartId && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
                variant => variant.Id == reservation.ProductVariantId,
                cancellationToken);
            if (variant is not null)
            {
                variant.ReleaseReservation(reservation.Quantity);
            }

            reservation.Cancel(now);
        }
    }

    private static PaymentInitiationResponse Map(Payment payment, Uri? checkoutUrl) =>
        new(
            payment.Id,
            payment.OrderId,
            payment.Provider,
            payment.ProviderReference,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            checkoutUrl);

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private static bool IsPaidStatus(string status) =>
        string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Authorized", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedStatus(string status) =>
        string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
}
