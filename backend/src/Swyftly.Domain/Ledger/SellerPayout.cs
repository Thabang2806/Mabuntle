using Swyftly.Domain.Common;

namespace Swyftly.Domain.Ledger;

public sealed class SellerPayout : AuditableEntity
{
    private readonly List<SellerPayoutItem> _items = [];

    private SellerPayout()
    {
    }

    public SellerPayout(Guid sellerId, decimal amount, string currency, DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        SellerId = sellerId;
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Status = SellerPayoutStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public SellerPayoutStatus Status { get; private set; }

    public DateTimeOffset? HeldAtUtc { get; private set; }

    public string? HeldByUserId { get; private set; }

    public string? HoldReason { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public string? ReleasedByUserId { get; private set; }

    public string? ReleaseReason { get; private set; }

    public IReadOnlyCollection<SellerPayoutItem> Items => _items.AsReadOnly();

    public void AddItem(Guid ledgerEntryId, Guid? orderId, Guid? paymentId, decimal amount, DateTimeOffset createdAtUtc)
    {
        if (ledgerEntryId == Guid.Empty)
        {
            throw new ArgumentException("Ledger entry id is required.", nameof(ledgerEntryId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        _items.Add(new SellerPayoutItem(Id, ledgerEntryId, orderId, paymentId, amount, Currency, createdAtUtc));
    }

    public void Hold(string actorUserId, string reason, DateTimeOffset heldAtUtc)
    {
        if (Status is SellerPayoutStatus.OnHold)
        {
            return;
        }

        if (Status is not (SellerPayoutStatus.Pending or SellerPayoutStatus.Available))
        {
            throw new InvalidOperationException("Only pending or available payouts can be held.");
        }

        Status = SellerPayoutStatus.OnHold;
        HeldByUserId = Required(actorUserId, nameof(actorUserId));
        HoldReason = Required(reason, nameof(reason));
        HeldAtUtc = heldAtUtc;
        UpdatedAtUtc = heldAtUtc;
    }

    public void Release(string actorUserId, string reason, DateTimeOffset releasedAtUtc)
    {
        if (Status != SellerPayoutStatus.OnHold)
        {
            throw new InvalidOperationException("Only held payouts can be released.");
        }

        Status = SellerPayoutStatus.Pending;
        ReleasedByUserId = Required(actorUserId, nameof(actorUserId));
        ReleaseReason = Required(reason, nameof(reason));
        ReleasedAtUtc = releasedAtUtc;
        UpdatedAtUtc = releasedAtUtc;
    }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }
}
