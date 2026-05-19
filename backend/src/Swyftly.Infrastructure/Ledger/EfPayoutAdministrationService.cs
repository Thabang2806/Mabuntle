using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Ledger;
using Swyftly.Domain.Ledger;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Ledger;

public sealed class EfPayoutAdministrationService(
    SwyftlyDbContext dbContext,
    IAuditLogService auditLogService,
    TimeProvider timeProvider) : IPayoutAdministrationService
{
    public async Task<Result<SellerPayoutResult>> HoldAsync(
        PayoutHoldRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return Result<SellerPayoutResult>.Failure(Error.NotFound("Payouts.NotFound", "Seller payout was not found."));
        }

        if (payout.Status == SellerPayoutStatus.OnHold)
        {
            return Result<SellerPayoutResult>.Success(Map(payout));
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.Hold(request.ActorUserId.ToString(), request.Reason, now);
            balance.HoldPending(payout.Amount);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutHeld",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SellerPayoutResult>.Success(Map(payout));
    }

    public async Task<Result<SellerPayoutResult>> ReleaseAsync(
        PayoutReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return Result<SellerPayoutResult>.Failure(Error.NotFound("Payouts.NotFound", "Seller payout was not found."));
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.Release(request.ActorUserId.ToString(), request.Reason, now);
            balance.ReleaseHeldToPending(payout.Amount);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutReleased",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SellerPayoutResult>.Success(Map(payout));
    }

    private async Task<SellerPayout?> GetPayoutAsync(Guid payoutId, CancellationToken cancellationToken) =>
        await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .SingleOrDefaultAsync(payout => payout.Id == payoutId, cancellationToken);

    private async Task<SellerBalance> GetRequiredBalanceAsync(SellerPayout payout, CancellationToken cancellationToken) =>
        await dbContext.SellerBalances.SingleAsync(
            balance => balance.SellerId == payout.SellerId && balance.Currency == payout.Currency,
            cancellationToken);

    private async Task RecordAuditAsync(
        Guid actorUserId,
        string actorRole,
        string actionType,
        SellerPayout payout,
        SellerPayoutStatus previousStatus,
        SellerPayoutStatus newStatus,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId.ToString(),
                actorRole,
                actionType,
                "SellerPayout",
                payout.Id.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { status = newStatus.ToString() }),
                reason,
                ipAddress),
            cancellationToken);
    }

    private static SellerPayoutResult Map(SellerPayout payout) =>
        new(
            payout.Id,
            payout.SellerId,
            payout.Amount,
            payout.Currency,
            payout.Status.ToString(),
            payout.CreatedAtUtc,
            payout.HeldAtUtc,
            payout.HoldReason,
            payout.ReleasedAtUtc,
            payout.ReleaseReason);

    private static Result<SellerPayoutResult> Validation(string propertyName, string message) =>
        Result<SellerPayoutResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));
}
