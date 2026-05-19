using Swyftly.Domain.Common;

namespace Swyftly.Domain.Orders;

public sealed class Shipment : AuditableEntity
{
    private readonly List<ShipmentEvent> _events = [];

    private Shipment()
    {
    }

    public Shipment(Guid orderId, Guid sellerId, Guid buyerId, DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        OrderId = orderId;
        SellerId = sellerId;
        BuyerId = buyerId;
        Status = ShipmentStatus.AwaitingFulfilment;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        AddEvent("ShipmentCreated", "Shipment is awaiting fulfilment.", createdAtUtc);
    }

    public Guid OrderId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid BuyerId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public string? CarrierName { get; private set; }

    public string? TrackingNumber { get; private set; }

    public string? TrackingUrl { get; private set; }

    public DateTimeOffset? ShippedAtUtc { get; private set; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public IReadOnlyCollection<ShipmentEvent> Events => _events.AsReadOnly();

    public void UpdateTracking(
        string carrierName,
        string trackingNumber,
        string? trackingUrl,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        CarrierName = RequiredText(carrierName, nameof(carrierName), maxLength: 120);
        TrackingNumber = RequiredText(trackingNumber, nameof(trackingNumber), maxLength: 160);
        TrackingUrl = OptionalText(trackingUrl, maxLength: 500);
        UpdatedAtUtc = occurredAtUtc;
        AddEvent("TrackingUpdated", note ?? "Tracking details were updated.", occurredAtUtc);
    }

    public void MarkInTransit(DateTimeOffset shippedAtUtc, string? note = null)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.ReturnedToSender)
        {
            throw new InvalidOperationException("Delivered or returned shipments cannot be marked as in transit.");
        }

        Status = ShipmentStatus.InTransit;
        ShippedAtUtc ??= shippedAtUtc;
        UpdatedAtUtc = shippedAtUtc;
        AddEvent("ShipmentInTransit", note ?? "Shipment was marked as shipped.", shippedAtUtc);
    }

    public void MarkDelivered(DateTimeOffset deliveredAtUtc, string? note = null)
    {
        if (Status != ShipmentStatus.InTransit)
        {
            throw new InvalidOperationException("Only in-transit shipments can be marked as delivered.");
        }

        Status = ShipmentStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        UpdatedAtUtc = deliveredAtUtc;
        AddEvent("ShipmentDelivered", note ?? "Shipment was marked as delivered.", deliveredAtUtc);
    }

    private void AddEvent(string eventType, string? message, DateTimeOffset occurredAtUtc)
    {
        _events.Add(new ShipmentEvent(
            Id,
            Status,
            eventType,
            message,
            CarrierName,
            TrackingNumber,
            occurredAtUtc));
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
