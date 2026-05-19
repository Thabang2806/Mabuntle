using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Inventory;
using Swyftly.Application.Orders;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Orders;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Orders;

public sealed class EfOrderCreationService(
    SwyftlyDbContext dbContext,
    IInventoryReservationService inventoryReservationService) : IOrderCreationService
{
    public async Task<Result<OrderResult>> CreateFromCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailures = Validate(request);
        if (validationFailures.Count > 0)
        {
            return Result<OrderResult>.Failure(Error.Validation(validationFailures));
        }

        var cart = await GetCartAsync(request, cancellationToken);
        if (cart is null)
        {
            return Result<OrderResult>.Failure(
                Error.NotFound("Orders.CartNotFound", "Active cart was not found."));
        }

        var existingOrder = await dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .SingleOrDefaultAsync(
                order => order.CartId == cart.Id
                    && order.BuyerId == request.BuyerId
                    && order.Status == OrderStatus.PendingPayment,
                cancellationToken);
        if (existingOrder is not null)
        {
            return Result<OrderResult>.Success(Map(existingOrder));
        }

        if (cart.Items.Count == 0)
        {
            return Validation("cart", "Cart must contain at least one item before an order can be created.");
        }

        if (!cart.SellerId.HasValue)
        {
            return Validation("cart", "Cart must be associated with a seller before an order can be created.");
        }

        var reservationResult = await inventoryReservationService.ReserveCartAsync(
            new ReserveCartInventoryRequest(
                request.BuyerId,
                cart.Id,
                request.StartedAtUtc,
                request.ReservationDuration),
            cancellationToken);
        if (reservationResult.IsFailure)
        {
            return Result<OrderResult>.Failure(reservationResult.Error);
        }

        var order = new Order(
            cart.BuyerId,
            cart.SellerId.Value,
            cart.Id,
            request.StartedAtUtc,
            request.ShippingAmount,
            request.PlatformFeeAmount,
            request.DiscountAmount);

        foreach (var item in cart.Items.OrderBy(item => item.CreatedAtUtc))
        {
            order.AddItem(
                item.ProductId,
                item.ProductVariantId,
                item.ProductTitle,
                item.Sku,
                item.Size,
                item.Colour,
                item.UnitPrice,
                item.Quantity);
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    private async Task<Cart?> GetCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Carts
            .Include(cart => cart.Items)
            .Where(cart => cart.BuyerId == request.BuyerId && cart.Status == CartStatus.Active);

        return request.CartId.HasValue
            ? await query.SingleOrDefaultAsync(cart => cart.Id == request.CartId.Value, cancellationToken)
            : await query.SingleOrDefaultAsync(cancellationToken);
    }

    private static List<ValidationFailure> Validate(CreateOrderFromCartRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.BuyerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerId", "Buyer id is required."));
        }

        if (request.CartId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("cartId", "Cart id cannot be empty."));
        }

        if (request.ReservationDuration <= TimeSpan.Zero)
        {
            failures.Add(new ValidationFailure("reservationDuration", "Reservation duration must be positive."));
        }

        if (request.ShippingAmount < 0)
        {
            failures.Add(new ValidationFailure("shippingAmount", "Shipping amount cannot be negative."));
        }

        if (request.PlatformFeeAmount < 0)
        {
            failures.Add(new ValidationFailure("platformFeeAmount", "Platform fee amount cannot be negative."));
        }

        if (request.DiscountAmount < 0)
        {
            failures.Add(new ValidationFailure("discountAmount", "Discount amount cannot be negative."));
        }

        return failures;
    }

    private static Result<OrderResult> Validation(string propertyName, string message) =>
        Result<OrderResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

    public static OrderResult Map(Order order) =>
        new(
            order.Id,
            order.BuyerId,
            order.SellerId,
            order.CartId,
            order.Status.ToString(),
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResult(
                    item.Id,
                    item.ProductId,
                    item.ProductVariantId,
                    item.ProductTitle,
                    item.Sku,
                    item.Size,
                    item.Colour,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal))
                .ToArray(),
            order.ItemsSubtotal,
            order.ShippingAmount,
            order.PlatformFeeAmount,
            order.DiscountAmount,
            order.TotalAmount,
            order.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .Select(history => new OrderStatusHistoryResult(
                    history.Id,
                    history.PreviousStatus?.ToString(),
                    history.NewStatus.ToString(),
                    history.ChangedAtUtc,
                    history.Reason))
                .ToArray(),
            order.Shipments
                .OrderBy(shipment => shipment.CreatedAtUtc)
                .Select(shipment => new ShipmentResult(
                    shipment.Id,
                    shipment.Status.ToString(),
                    shipment.CarrierName,
                    shipment.TrackingNumber,
                    shipment.TrackingUrl,
                    shipment.ShippedAtUtc,
                    shipment.DeliveredAtUtc,
                    shipment.Events
                        .OrderBy(shipmentEvent => shipmentEvent.OccurredAtUtc)
                        .Select(shipmentEvent => new ShipmentEventResult(
                            shipmentEvent.Id,
                            shipmentEvent.Status.ToString(),
                            shipmentEvent.EventType,
                            shipmentEvent.Message,
                            shipmentEvent.CarrierName,
                            shipmentEvent.TrackingNumber,
                            shipmentEvent.OccurredAtUtc))
                        .ToArray()))
                .ToArray());
}
