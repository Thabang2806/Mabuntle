using Swyftly.Application.Common.Results;

namespace Swyftly.Application.Orders;

public interface IOrderCreationService
{
    Task<Result<OrderResult>> CreateFromCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateOrderFromCartRequest(
    Guid BuyerId,
    Guid? CartId,
    DateTimeOffset StartedAtUtc,
    TimeSpan ReservationDuration,
    decimal ShippingAmount = 0,
    decimal PlatformFeeAmount = 0,
    decimal DiscountAmount = 0);

public sealed record OrderResult(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    Guid CartId,
    string Status,
    IReadOnlyCollection<OrderItemResult> Items,
    decimal ItemsSubtotal,
    decimal ShippingAmount,
    decimal PlatformFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    IReadOnlyCollection<OrderStatusHistoryResult> StatusHistory,
    IReadOnlyCollection<ShipmentResult> Shipments);

public sealed record OrderItemResult(
    Guid OrderItemId,
    Guid ProductId,
    Guid ProductVariantId,
    string? ProductTitle,
    string Sku,
    string Size,
    string Colour,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderStatusHistoryResult(
    Guid StatusHistoryId,
    string? PreviousStatus,
    string NewStatus,
    DateTimeOffset ChangedAtUtc,
    string? Reason);

public sealed record ShipmentResult(
    Guid ShipmentId,
    string Status,
    string? CarrierName,
    string? TrackingNumber,
    string? TrackingUrl,
    DateTimeOffset? ShippedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    IReadOnlyCollection<ShipmentEventResult> Events);

public sealed record ShipmentEventResult(
    Guid ShipmentEventId,
    string Status,
    string EventType,
    string? Message,
    string? CarrierName,
    string? TrackingNumber,
    DateTimeOffset OccurredAtUtc);
