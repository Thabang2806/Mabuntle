using Swyftly.Domain.Common;

namespace Swyftly.Domain.Orders;

public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];
    private readonly List<Shipment> _shipments = [];

    private Order()
    {
    }

    public Order(
        Guid buyerId,
        Guid sellerId,
        Guid cartId,
        DateTimeOffset createdAtUtc,
        decimal shippingAmount = 0,
        decimal platformFeeAmount = 0,
        decimal discountAmount = 0)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (cartId == Guid.Empty)
        {
            throw new ArgumentException("Cart id is required.", nameof(cartId));
        }

        ValidateMoney(shippingAmount, nameof(shippingAmount), allowNegative: false);
        ValidateMoney(platformFeeAmount, nameof(platformFeeAmount), allowNegative: false);
        ValidateMoney(discountAmount, nameof(discountAmount), allowNegative: false);

        BuyerId = buyerId;
        SellerId = sellerId;
        CartId = cartId;
        Status = OrderStatus.PendingPayment;
        ShippingAmount = shippingAmount;
        PlatformFeeAmount = platformFeeAmount;
        DiscountAmount = discountAmount;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        AddStatusHistory(null, Status, createdAtUtc, "OrderCreated");
    }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid CartId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal ShippingAmount { get; private set; }

    public decimal PlatformFeeAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<Shipment> Shipments => _shipments.AsReadOnly();

    public decimal ItemsSubtotal => _items.Sum(item => item.LineTotal);

    public decimal TotalAmount => ItemsSubtotal + ShippingAmount + PlatformFeeAmount - DiscountAmount;

    public void AddItem(
        Guid productId,
        Guid productVariantId,
        string? productTitle,
        string sku,
        string size,
        string colour,
        decimal unitPrice,
        int quantity)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOperationException("Items can only be added while an order is pending payment.");
        }

        _items.Add(new OrderItem(
            Id,
            productId,
            productVariantId,
            productTitle,
            sku,
            size,
            colour,
            unitPrice,
            quantity));
    }

    public void ChangeStatus(OrderStatus newStatus, DateTimeOffset changedAtUtc, string? reason = null)
    {
        if (newStatus == Status)
        {
            return;
        }

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = changedAtUtc;
        AddStatusHistory(previousStatus, newStatus, changedAtUtc, reason);
    }

    private void AddStatusHistory(
        OrderStatus? previousStatus,
        OrderStatus newStatus,
        DateTimeOffset changedAtUtc,
        string? reason)
    {
        _statusHistory.Add(new OrderStatusHistory(Id, previousStatus, newStatus, changedAtUtc, reason));
    }

    private static void ValidateMoney(decimal amount, string parameterName, bool allowNegative)
    {
        if (!allowNegative && amount < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount cannot be negative.");
        }
    }
}
