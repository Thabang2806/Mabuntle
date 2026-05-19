using Swyftly.Domain.Support;

namespace Swyftly.UnitTests.Domain;

public sealed class SupportTicketTests
{
    [Fact]
    public void Constructor_OpensTicket_AndAddsInitialPublicMessage()
    {
        var userId = Guid.NewGuid();

        var ticket = new SupportTicket(
            userId,
            "Buyer",
            Guid.NewGuid(),
            null,
            SupportTicketCategory.OrderIssue,
            "Order issue",
            "My order has a problem.",
            Guid.NewGuid(),
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.Equal(SupportTicketStatus.Open, ticket.Status);
        Assert.Equal(SupportTicketCategory.OrderIssue, ticket.Category);
        var message = Assert.Single(ticket.Messages);
        Assert.False(message.IsInternal);
        Assert.Equal("My order has a problem.", message.Message);
    }

    [Fact]
    public void SupportResponse_SetsWaitingStatusForOriginalRequester()
    {
        var ticket = CreateSellerTicket();

        ticket.AddSupportResponse(Guid.NewGuid(), "SupportAgent", "Please send the invoice.", DateTimeOffset.UtcNow);

        Assert.Equal(SupportTicketStatus.WaitingForSeller, ticket.Status);
        Assert.Equal(2, ticket.Messages.Count);
    }

    [Fact]
    public void InternalNote_DoesNotChangeWaitingStatus()
    {
        var ticket = CreateSellerTicket();
        ticket.AddSupportResponse(Guid.NewGuid(), "SupportAgent", "Please send the invoice.", DateTimeOffset.UtcNow);

        ticket.AddInternalNote(Guid.NewGuid(), "Admin", "Review seller history.", DateTimeOffset.UtcNow);

        Assert.Equal(SupportTicketStatus.WaitingForSeller, ticket.Status);
        Assert.Contains(ticket.Messages, message => message.IsInternal && message.Message == "Review seller history.");
    }

    [Fact]
    public void ResolvedTicket_CannotReceivePublicMessages()
    {
        var ticket = CreateSellerTicket();
        ticket.Resolve(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ticket.AddCustomerMessage(Guid.NewGuid(), "Seller", "More detail.", DateTimeOffset.UtcNow));

        Assert.Equal("Resolved or closed support tickets cannot receive public messages.", exception.Message);
    }

    private static SupportTicket CreateSellerTicket() =>
        new(
            Guid.NewGuid(),
            "Seller",
            null,
            Guid.NewGuid(),
            SupportTicketCategory.PaymentIssue,
            "Payment issue",
            "Payout is delayed.",
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);
}
