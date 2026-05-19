using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Swyftly.Application.Inventory;
using Swyftly.Application.Orders;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Orders;
using Swyftly.Infrastructure.Inventory;
using Swyftly.Infrastructure.Orders;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.UnitTests.Infrastructure;

public class OrderCreationServiceTests
{
    [Fact]
    public async Task CreateFromCartAsync_CreatesPendingPaymentOrderAndSnapshotsCartItems()
    {
        await using var dbContext = CreateDbContext();
        var (buyer, product, variant, cart) = await SeedCartAsync(dbContext, price: 499m, quantity: 2);
        var service = CreateService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var result = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt,
            TimeSpan.FromMinutes(15)));

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingPayment", result.Value.Status);
        Assert.Equal(cart.Id, result.Value.CartId);
        Assert.Equal(product.SellerId, result.Value.SellerId);
        Assert.Equal(998m, result.Value.TotalAmount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal("Cotton Dress", item.ProductTitle);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(499m, item.UnitPrice);
        Assert.Equal(998m, item.LineTotal);
        Assert.Single(result.Value.StatusHistory);

        var reservation = await dbContext.InventoryReservations.SingleAsync();
        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);
        Assert.Equal(2, variant.ReservedQuantity);
    }

    [Fact]
    public async Task CreateFromCartAsync_ReturnsExistingPendingPaymentOrderForSameCart()
    {
        await using var dbContext = CreateDbContext();
        var (buyer, _, _, cart) = await SeedCartAsync(dbContext, price: 499m, quantity: 1);
        var service = CreateService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var first = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt,
            TimeSpan.FromMinutes(15)));

        var second = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt.AddMinutes(1),
            TimeSpan.FromMinutes(15)));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.OrderId, second.Value.OrderId);
        Assert.Equal(1, await dbContext.Orders.CountAsync());
        Assert.Equal(1, await dbContext.InventoryReservations.CountAsync());
    }

    [Fact]
    public async Task CreateFromCartAsync_ReturnsValidationFailureForEmptyCart()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
            TimeSpan.FromMinutes(15)));

        Assert.True(result.IsFailure);
        Assert.Contains("at least one item", result.Error.Details!["cart"].Single());
    }

    private static EfOrderCreationService CreateService(SwyftlyDbContext dbContext) =>
        new(dbContext, new EfInventoryReservationService(dbContext));

    private static async Task<(BuyerProfile Buyer, Product Product, ProductVariant Variant, Cart Cart)> SeedCartAsync(
        SwyftlyDbContext dbContext,
        decimal price,
        int quantity)
    {
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", price, price + 100, stockQuantity: 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            product.SellerId,
            "Cotton Dress",
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            quantity,
            variant.AvailableQuantity);

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        return (buyer, product, variant, cart);
    }

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"OrderCreationServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SwyftlyDbContext(options);
    }
}
