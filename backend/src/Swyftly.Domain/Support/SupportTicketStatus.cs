namespace Swyftly.Domain.Support;

public enum SupportTicketStatus
{
    Open = 0,
    WaitingForCustomer,
    WaitingForSeller,
    Escalated,
    Resolved,
    Closed
}
