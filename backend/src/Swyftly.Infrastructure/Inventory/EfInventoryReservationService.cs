using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Inventory;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Inventory;

public sealed class EfInventoryReservationService(SwyftlyDbContext dbContext) : IInventoryReservationService
{
    public async Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ReserveCartAsync(
        ReserveCartInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BuyerId == Guid.Empty)
        {
            return Validation("buyerId", "Buyer id is required.");
        }

        if (request.CartId == Guid.Empty)
        {
            return Validation("cartId", "Cart id is required.");
        }

        if (request.ReservationDuration <= TimeSpan.Zero)
        {
            return Validation("reservationDuration", "Reservation duration must be positive.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var cart = await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.Id == request.CartId
                    && cart.BuyerId == request.BuyerId
                    && cart.Status == CartStatus.Active,
                cancellationToken);
        if (cart is null)
        {
            return Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(
                Error.NotFound("InventoryReservations.CartNotFound", "Active cart was not found."));
        }

        if (cart.Items.Count == 0)
        {
            return Validation("cart", "Cart must contain at least one item before inventory can be reserved.");
        }

        var existingActiveReservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == cart.Id && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);

        var variants = new Dictionary<Guid, ProductVariant>();
        var existingReservedVariants = new Dictionary<Guid, ProductVariant>();
        foreach (var reservation in existingActiveReservations)
        {
            var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
                variant => variant.Id == reservation.ProductVariantId,
                cancellationToken);
            if (variant is not null)
            {
                existingReservedVariants[variant.Id] = variant;
            }
        }

        foreach (var item in cart.Items)
        {
            var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
                variant => variant.Id == item.ProductVariantId,
                cancellationToken);
            if (variant is null || variant.Status != ProductVariantStatus.Active)
            {
                return Validation("cart", $"Product variant {item.ProductVariantId} is not available.");
            }

            var existingQuantity = existingActiveReservations
                .Where(reservation => reservation.ProductVariantId == item.ProductVariantId)
                .Sum(reservation => reservation.Quantity);
            if (item.Quantity > variant.AvailableQuantity + existingQuantity)
            {
                return Validation("cart", $"Insufficient stock for product variant {item.ProductVariantId}.");
            }

            variants[variant.Id] = variant;
        }

        foreach (var reservation in existingActiveReservations)
        {
            if (existingReservedVariants.TryGetValue(reservation.ProductVariantId, out var reservedVariant))
            {
                reservedVariant.ReleaseReservation(reservation.Quantity);
            }

            reservation.Cancel(request.StartedAtUtc);
        }

        var expiresAtUtc = request.StartedAtUtc.Add(request.ReservationDuration);
        var results = new List<InventoryReservationResult>();

        foreach (var item in cart.Items)
        {
            var variant = variants[item.ProductVariantId];
            variant.Reserve(item.Quantity);
            var reservation = new InventoryReservation(
                variant.Id,
                cart.BuyerId,
                cart.Id,
                item.Quantity,
                expiresAtUtc,
                request.StartedAtUtc);
            dbContext.InventoryReservations.Add(reservation);
            results.Add(Map(reservation));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<IReadOnlyCollection<InventoryReservationResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ExpireReservationsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.Status == InventoryReservationStatus.Active
                && reservation.ExpiresAtUtc <= utcNow)
            .OrderBy(reservation => reservation.ExpiresAtUtc)
            .ToListAsync(cancellationToken);
        var results = new List<InventoryReservationResult>();

        foreach (var reservation in reservations)
        {
            var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
                variant => variant.Id == reservation.ProductVariantId,
                cancellationToken);
            if (variant is not null)
            {
                variant.ReleaseReservation(reservation.Quantity);
            }

            reservation.Expire(utcNow);
            results.Add(Map(reservation));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<IReadOnlyCollection<InventoryReservationResult>>.Success(results);
    }

    private static InventoryReservationResult Map(InventoryReservation reservation) =>
        new(
            reservation.Id,
            reservation.ProductVariantId,
            reservation.BuyerId,
            reservation.CartId,
            reservation.Quantity,
            reservation.Status.ToString(),
            reservation.ExpiresAtUtc);

    private static Result<IReadOnlyCollection<InventoryReservationResult>> Validation(string propertyName, string message) =>
        Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));
}
