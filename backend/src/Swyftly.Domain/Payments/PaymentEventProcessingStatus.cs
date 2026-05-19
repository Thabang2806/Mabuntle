namespace Swyftly.Domain.Payments;

public enum PaymentEventProcessingStatus
{
    Received = 0,
    Processed,
    Duplicate,
    Failed
}
