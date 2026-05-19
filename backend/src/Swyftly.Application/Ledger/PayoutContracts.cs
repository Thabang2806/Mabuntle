using Swyftly.Application.Common.Results;

namespace Swyftly.Application.Ledger;

public interface IPayoutAdministrationService
{
    Task<Result<SellerPayoutResult>> HoldAsync(
        PayoutHoldRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SellerPayoutResult>> ReleaseAsync(
        PayoutReleaseRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PayoutHoldRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutReleaseRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record SellerPayoutResult(
    Guid PayoutId,
    Guid SellerId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? HeldAtUtc,
    string? HoldReason,
    DateTimeOffset? ReleasedAtUtc,
    string? ReleaseReason);
