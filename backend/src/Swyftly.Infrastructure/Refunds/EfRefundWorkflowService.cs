using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Payments;
using Swyftly.Application.Refunds;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Returns;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Refunds;

public sealed class EfRefundWorkflowService(
    SwyftlyDbContext dbContext,
    IPaymentProvider paymentProvider,
    IAuditLogService auditLogService) : IRefundWorkflowService
{
    public async Task<Result<RefundResult>> CreateRefundRequestAsync(
        CreateRefundWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation.Count > 0)
        {
            return Result<RefundResult>.Failure(Error.Validation(validation));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleOrDefaultAsync(existing => existing.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.OrderNotFound", "Order was not found."));
        }

        var payment = await dbContext.Payments
            .SingleOrDefaultAsync(
                existing => existing.OrderId == order.Id && existing.Status != PaymentStatus.Failed,
                cancellationToken);
        if (payment is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.PaymentNotFound", "Payment was not found."));
        }

        if (payment.Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            return Result<RefundResult>.Failure(
                Error.Conflict("Refunds.PaymentNotRefundable", "Only paid payments can be refunded."));
        }

        var totalAlreadyRefunded = await TotalRefundedOrPendingAsync(payment.Id, null, cancellationToken);
        if (totalAlreadyRefunded + request.Amount > payment.Amount)
        {
            return Validation("amount", "Refund amount cannot exceed the remaining refundable payment amount.");
        }

        ReturnRequest? returnRequest = null;
        if (request.ReturnRequestId.HasValue)
        {
            returnRequest = await dbContext.ReturnRequests
                .SingleOrDefaultAsync(
                    existing => existing.Id == request.ReturnRequestId.Value && existing.OrderId == order.Id,
                    cancellationToken);
            if (returnRequest is null)
            {
                return Result<RefundResult>.Failure(Error.NotFound("Refunds.ReturnNotFound", "Return request was not found."));
            }

            try
            {
                if (returnRequest.Status != ReturnStatus.RefundPending)
                {
                    returnRequest.MarkRefundPending(request.RequestedAtUtc);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Validation("returnRequest", exception.Message);
            }
        }

        var refund = new Refund(
            order.Id,
            payment.Id,
            order.BuyerId,
            order.SellerId,
            request.ReturnRequestId,
            request.Amount,
            payment.Currency,
            request.Reason,
            request.RequestedAtUtc);

        dbContext.Refunds.Add(refund);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RefundResult>.Success(Map(refund));
    }

    public async Task<Result<RefundResult>> ApproveRefundAsync(
        ApproveRefundWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Approval reason is required.");
        }

        var refund = await dbContext.Refunds
            .Include(existing => existing.Events)
            .SingleOrDefaultAsync(existing => existing.Id == request.RefundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.NotFound", "Refund was not found."));
        }

        if (refund.Status == RefundStatus.Refunded)
        {
            return Result<RefundResult>.Success(Map(refund));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleAsync(existing => existing.Id == refund.OrderId, cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(existing => existing.Id == refund.PaymentId, cancellationToken);
        var returnRequest = refund.ReturnRequestId.HasValue
            ? await dbContext.ReturnRequests.SingleOrDefaultAsync(existing => existing.Id == refund.ReturnRequestId.Value, cancellationToken)
            : null;

        try
        {
            refund.Approve(request.ActorUserId, request.Reason, request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            refund.MarkProcessing(request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("refund", exception.Message);
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderReference))
        {
            refund.MarkFailed("Payment provider reference is missing.", request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Validation("payment", "Payment provider reference is missing.");
        }

        var providerResult = await paymentProvider.RefundPaymentAsync(
            new PaymentRefundRequest(
                payment.ProviderReference,
                refund.Amount,
                refund.Currency,
                request.Reason,
                new Dictionary<string, string>
                {
                    ["refundId"] = refund.Id.ToString(),
                    ["orderId"] = refund.OrderId.ToString()
                }),
            cancellationToken);

        if (providerResult.IsFailure)
        {
            refund.MarkFailed(providerResult.Error.Description, request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<RefundResult>.Failure(providerResult.Error);
        }

        var providerRefund = providerResult.Value;
        try
        {
            refund.MarkRefunded(providerRefund.ProviderRefundReference, providerRefund.RefundedAtUtc);
            TrackLatestRefundEvent(refund);
            await CreateLedgerReversalsAndAdjustBalancesAsync(refund, payment, order, providerRefund.RefundedAtUtc, cancellationToken);
            var totalRefunded = await TotalRefundedOrPendingAsync(payment.Id, refund.Id, cancellationToken) + refund.Amount;
            payment.ApplyRefund(totalRefunded, providerRefund.RefundedAtUtc);
            if (payment.Status == PaymentStatus.Refunded)
            {
                order.ChangeStatus(OrderStatus.Refunded, providerRefund.RefundedAtUtc, "RefundApproved");
                TrackLatestOrderStatusHistory(order);
            }

            if (returnRequest is not null)
            {
                returnRequest.MarkRefunded(providerRefund.RefundedAtUtc);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("refund", exception.Message);
        }

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                request.ActorUserId.ToString(),
                request.ActorRole,
                "RefundApproved",
                "Refund",
                refund.Id.ToString(),
                JsonSerializer.Serialize(new { status = RefundStatus.Requested.ToString() }),
                JsonSerializer.Serialize(new { status = RefundStatus.Refunded.ToString(), amount = refund.Amount }),
                request.Reason,
                request.IpAddress),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RefundResult>.Success(Map(refund));
    }

    private async Task CreateLedgerReversalsAndAdjustBalancesAsync(
        Refund refund,
        Payment payment,
        Order order,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var paymentEntries = await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == payment.Id)
            .ToListAsync(cancellationToken);
        var ratio = refund.Amount / payment.Amount;
        var sellerOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.SellerPendingBalanceCredited)
            .Sum(entry => entry.Amount);
        var platformOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.PlatformCommissionRecorded)
            .Sum(entry => entry.Amount);
        var providerFeeOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.PaymentProviderFeeRecorded)
            .Sum(entry => entry.Amount);
        var sellerDebit = RoundMoney(sellerOriginal * ratio);
        var platformDebit = RoundMoney(platformOriginal * ratio);
        var providerFeeCredit = RoundMoney(providerFeeOriginal * ratio);

        dbContext.LedgerEntries.AddRange(
            new LedgerEntry(
                order.Id,
                null,
                order.SellerId,
                order.BuyerId,
                payment.Id,
                LedgerEntryType.RefundIssued,
                refund.Amount,
                refund.Currency,
                LedgerDirection.Debit,
                $"Refund issued for refund {refund.Id}.",
                createdAtUtc),
            new LedgerEntry(
                order.Id,
                null,
                order.SellerId,
                order.BuyerId,
                payment.Id,
                LedgerEntryType.RefundReversal,
                sellerDebit,
                refund.Currency,
                LedgerDirection.Debit,
                "Seller balance refund reversal.",
                createdAtUtc),
            new LedgerEntry(
                order.Id,
                null,
                order.SellerId,
                order.BuyerId,
                payment.Id,
                LedgerEntryType.RefundReversal,
                platformDebit,
                refund.Currency,
                LedgerDirection.Debit,
                "Platform commission refund reversal.",
                createdAtUtc),
            new LedgerEntry(
                order.Id,
                null,
                order.SellerId,
                order.BuyerId,
                payment.Id,
                LedgerEntryType.RefundReversal,
                providerFeeCredit,
                refund.Currency,
                LedgerDirection.Credit,
                "Payment provider fee refund reversal.",
                createdAtUtc));

        var balance = await dbContext.SellerBalances.SingleOrDefaultAsync(
            existing => existing.SellerId == order.SellerId && existing.Currency == refund.Currency,
            cancellationToken);
        if (balance is null)
        {
            balance = new SellerBalance(order.SellerId, refund.Currency);
            dbContext.SellerBalances.Add(balance);
        }

        if (sellerDebit > 0)
        {
            balance.ApplyRefundDebit(sellerDebit);
        }
    }

    private async Task<decimal> TotalRefundedOrPendingAsync(
        Guid paymentId,
        Guid? excludeRefundId,
        CancellationToken cancellationToken) =>
        await dbContext.Refunds
            .Where(refund => refund.PaymentId == paymentId
                && refund.Id != excludeRefundId
                && refund.Status != RefundStatus.Failed
                && refund.Status != RefundStatus.Rejected)
            .SumAsync(refund => refund.Amount, cancellationToken);

    private void TrackLatestRefundEvent(Refund refund)
    {
        var refundEvent = refund.Events.LastOrDefault();
        if (refundEvent is not null)
        {
            dbContext.RefundEvents.Add(refundEvent);
        }
    }

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private static List<ValidationFailure> ValidateCreateRequest(CreateRefundWorkflowRequest request)
    {
        var failures = new List<ValidationFailure>();
        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (request.ReturnRequestId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("returnRequestId", "Return request id cannot be empty."));
        }

        if (request.Amount <= 0)
        {
            failures.Add(new ValidationFailure("amount", "Refund amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            failures.Add(new ValidationFailure("reason", "Refund reason is required."));
        }

        if (request.ActorUserId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("actorUserId", "Actor user id is required."));
        }

        return failures;
    }

    private static Result<RefundResult> Validation(string propertyName, string message) =>
        Result<RefundResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

    public static RefundResult Map(Refund refund) =>
        new(
            refund.Id,
            refund.OrderId,
            refund.PaymentId,
            refund.BuyerId,
            refund.SellerId,
            refund.ReturnRequestId,
            refund.Amount,
            refund.Currency,
            refund.Status.ToString(),
            refund.Reason,
            refund.ProviderRefundReference,
            refund.FailureReason,
            refund.RequestedAtUtc,
            refund.ApprovedAtUtc,
            refund.RefundedAtUtc,
            refund.Events
                .OrderBy(refundEvent => refundEvent.CreatedAtUtc)
                .Select(refundEvent => new RefundEventResult(
                    refundEvent.Id,
                    refundEvent.Status.ToString(),
                    refundEvent.EventType,
                    refundEvent.Message,
                    refundEvent.CreatedAtUtc))
                .ToArray());

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
