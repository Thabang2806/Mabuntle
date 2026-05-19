using Swyftly.Domain.Common;

namespace Swyftly.Domain.Support;

public sealed class SupportTicket : AuditableEntity
{
    private readonly List<SupportMessage> _messages = [];

    private SupportTicket()
    {
    }

    public SupportTicket(
        Guid createdByUserId,
        string createdByRole,
        Guid? buyerId,
        Guid? sellerId,
        SupportTicketCategory category,
        string subject,
        string description,
        Guid? linkedOrderId,
        Guid? linkedProductId,
        Guid? linkedSellerId,
        Guid? linkedPaymentId,
        DateTimeOffset openedAtUtc)
    {
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created-by user id is required.", nameof(createdByUserId));
        }

        CreatedByUserId = createdByUserId;
        CreatedByRole = Required(createdByRole, nameof(createdByRole), 64);
        BuyerId = buyerId == Guid.Empty ? null : buyerId;
        SellerId = sellerId == Guid.Empty ? null : sellerId;
        Category = category;
        Status = SupportTicketStatus.Open;
        Subject = Required(subject, nameof(subject), 200);
        Description = Required(description, nameof(description), 4000);
        LinkedOrderId = linkedOrderId == Guid.Empty ? null : linkedOrderId;
        LinkedProductId = linkedProductId == Guid.Empty ? null : linkedProductId;
        LinkedSellerId = linkedSellerId == Guid.Empty ? null : linkedSellerId;
        LinkedPaymentId = linkedPaymentId == Guid.Empty ? null : linkedPaymentId;
        OpenedAtUtc = openedAtUtc;
        CreatedAtUtc = openedAtUtc;
        UpdatedAtUtc = openedAtUtc;

        AddCustomerMessage(createdByUserId, CreatedByRole, Description, openedAtUtc);
    }

    public Guid CreatedByUserId { get; private set; }

    public string CreatedByRole { get; private set; } = string.Empty;

    public Guid? BuyerId { get; private set; }

    public Guid? SellerId { get; private set; }

    public SupportTicketCategory Category { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid? LinkedOrderId { get; private set; }

    public Guid? LinkedProductId { get; private set; }

    public Guid? LinkedSellerId { get; private set; }

    public Guid? LinkedPaymentId { get; private set; }

    public Guid? AssignedSupportUserId { get; private set; }

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public IReadOnlyCollection<SupportMessage> Messages => _messages.AsReadOnly();

    public bool IsClosed => Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed;

    public void AddCustomerMessage(Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        EnsureCanAddPublicMessage();
        _messages.Add(new SupportMessage(Id, senderUserId, senderRole, message, isInternal: false, createdAtUtc));
        Status = SupportTicketStatus.Open;
        UpdatedAtUtc = createdAtUtc;
    }

    public void AddSupportResponse(Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        EnsureCanAddPublicMessage();
        _messages.Add(new SupportMessage(Id, senderUserId, senderRole, message, isInternal: false, createdAtUtc));
        AssignedSupportUserId ??= senderUserId;
        Status = string.Equals(CreatedByRole, "Seller", StringComparison.OrdinalIgnoreCase)
            ? SupportTicketStatus.WaitingForSeller
            : SupportTicketStatus.WaitingForCustomer;
        UpdatedAtUtc = createdAtUtc;
    }

    public void AddInternalNote(Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new InvalidOperationException("Closed support tickets cannot be changed.");
        }

        _messages.Add(new SupportMessage(Id, senderUserId, senderRole, message, isInternal: true, createdAtUtc));
        AssignedSupportUserId ??= senderUserId;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Escalate(DateTimeOffset changedAtUtc)
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new InvalidOperationException("Closed support tickets cannot be escalated.");
        }

        Status = SupportTicketStatus.Escalated;
        UpdatedAtUtc = changedAtUtc;
    }

    public void Resolve(DateTimeOffset resolvedAtUtc)
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new InvalidOperationException("Closed support tickets cannot be resolved.");
        }

        Status = SupportTicketStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
        UpdatedAtUtc = resolvedAtUtc;
    }

    public void Close(DateTimeOffset closedAtUtc)
    {
        Status = SupportTicketStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        UpdatedAtUtc = closedAtUtc;
    }

    private void EnsureCanAddPublicMessage()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Resolved or closed support tickets cannot receive public messages.");
        }
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
