using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Infrastructure.Analytics;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.UnitTests.Infrastructure;

public class BuyerGrowthOutcomeAttributionServiceTests
{
    [Fact]
    public async Task RecordProductAddedToCartAsync_UsesRecentProductOpenAndIsIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.Parse("2026-05-29T12:00:00Z");
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        var variantId = Guid.NewGuid();
        cart.AddOrUpdateItem(
            product.Id,
            variantId,
            product.SellerId,
            "Rose dress",
            "ROSE-M",
            "M",
            "Rose",
            799m,
            1,
            5);
        var growthEvent = new BuyerGrowthEvent(
            buyer.Id,
            BuyerGrowthEventType.AssistantProductOpened,
            BuyerGrowthSourceTool.Assistant,
            now.AddMinutes(-5),
            product.Id,
            confidenceBand: BuyerGrowthConfidenceBand.High);
        dbContext.AddRange(buyer, product, cart, growthEvent);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await service.RecordProductAddedToCartAsync(buyer.Id, product.Id, cart.Id, now);
        await service.RecordProductAddedToCartAsync(buyer.Id, product.Id, cart.Id, now.AddSeconds(10));

        var outcome = await dbContext.BuyerGrowthOutcomes.SingleAsync();
        Assert.Equal(BuyerGrowthOutcomeType.ProductAddedToCart, outcome.OutcomeType);
        Assert.Equal(BuyerGrowthSourceTool.Assistant, outcome.SourceTool);
        Assert.Equal(product.Id, outcome.ProductId);
        Assert.Equal(cart.Id, outcome.CartId);
        Assert.Equal(growthEvent.Id, outcome.SourceEventId);
    }

    [Fact]
    public async Task RecordOrderPaidAsync_RecordsOneOutcomePerAttributedProductAndIsIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.Parse("2026-05-29T12:00:00Z");
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        var order = new Order(buyer.Id, product.SellerId, cart.Id, now);
        order.AddItem(product.Id, Guid.NewGuid(), "Rose dress", "ROSE-M", "M", "Rose", 799m, 1);
        var growthEvent = new BuyerGrowthEvent(
            buyer.Id,
            BuyerGrowthEventType.VisualProductOpened,
            BuyerGrowthSourceTool.VisualSearch,
            now.AddMinutes(-10),
            product.Id,
            confidenceBand: BuyerGrowthConfidenceBand.Medium);
        dbContext.AddRange(buyer, product, cart, order, growthEvent);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await service.RecordOrderPaidAsync(order.Id, now);
        await service.RecordOrderPaidAsync(order.Id, now.AddSeconds(10));

        var outcome = await dbContext.BuyerGrowthOutcomes.SingleAsync();
        Assert.Equal(BuyerGrowthOutcomeType.OrderPaid, outcome.OutcomeType);
        Assert.Equal(BuyerGrowthSourceTool.VisualSearch, outcome.SourceTool);
        Assert.Equal(order.Id, outcome.OrderId);
        Assert.Equal(product.Id, outcome.ProductId);
        Assert.Equal(growthEvent.Id, outcome.SourceEventId);
    }

    private static EfBuyerGrowthOutcomeAttributionService CreateService(SwyftlyDbContext dbContext) =>
        new(dbContext, NullLogger<EfBuyerGrowthOutcomeAttributionService>.Instance);

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"BuyerGrowthOutcomeAttributionServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new SwyftlyDbContext(options);
    }
}
