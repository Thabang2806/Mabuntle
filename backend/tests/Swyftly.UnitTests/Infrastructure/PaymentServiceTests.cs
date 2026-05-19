using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Swyftly.Application.Ledger;
using Swyftly.Application.Payments;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Infrastructure.Advertising;
using Swyftly.Infrastructure.Ledger;
using Swyftly.Infrastructure.Payments;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.UnitTests.Infrastructure;

public class PaymentServiceTests
{
    [Fact]
    public async Task InitiatePaymentAsync_CreatesPendingPaymentAndStoresProviderReference()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var service = CreateService(dbContext, new PaymentProviderOptions());

        var result = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.StartsWith("fake_", result.Value.ProviderReference, StringComparison.Ordinal);
        var payment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(seed.Order.TotalAmount, payment.Amount);
        Assert.Equal(result.Value.ProviderReference, payment.ProviderReference);
    }

    [Fact]
    public async Task InitiatePaymentAsync_MarksPaymentFailedWhenProviderFails()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var service = CreateService(dbContext, new PaymentProviderOptions
        {
            FakeOutcome = FakePaymentOutcomes.Failure
        });

        var result = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(result.IsFailure);
        var payment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Null(payment.ProviderReference);
    }

    [Fact]
    public async Task ProcessWebhookAsync_SuccessMarksPaymentOrderReservationsAndLedgerOnce()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = PaidPayload("evt_1", "fake_reference");
        var webhook = new ProcessPaymentWebhookRequest("Fake", payload, SignedHeaders(payload, options.WebhookSigningSecret));

        var first = await service.ProcessWebhookAsync(webhook);
        var duplicate = await service.ProcessWebhookAsync(webhook);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Paid, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Equal(InventoryReservationStatus.Confirmed, (await dbContext.InventoryReservations.SingleAsync()).Status);
        Assert.Equal(4, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == payment.Id));
        Assert.Equal(1, await dbContext.PaymentEvents.CountAsync());
        Assert.Equal(PaymentEventProcessingStatus.Processed, (await dbContext.PaymentEvents.SingleAsync()).ProcessingStatus);
    }

    [Fact]
    public async Task ProcessWebhookAsync_FailureReleasesReservationAndCancelsOrder()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_failed_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = FailedPayload("evt_2", "fake_failed_reference");

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            payload,
            SignedHeaders(payload, options.WebhookSigningSecret)));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Cancelled, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Equal(InventoryReservationStatus.Cancelled, (await dbContext.InventoryReservations.SingleAsync()).Status);
        Assert.Equal(0, (await dbContext.ProductVariants.SingleAsync()).ReservedQuantity);
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidSignatureDoesNotPersistPaymentEvent()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var service = CreateService(dbContext, SignedWebhookOptions());

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            PaidPayload("evt_invalid", "fake_reference"),
            new Dictionary<string, string>
            {
                [FakePaymentProvider.HeaderSignatureKey] = "invalid"
            }));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.InvalidWebhookSignature", result.Error.Code);
        Assert.Empty(await dbContext.PaymentEvents.ToListAsync());
        Assert.Equal(PaymentStatus.Pending, (await dbContext.Payments.SingleAsync()).Status);
    }

    private static EfPaymentService CreateService(SwyftlyDbContext dbContext, PaymentProviderOptions paymentOptions)
    {
        var provider = new FakePaymentProvider(Options.Create(paymentOptions), TimeProvider.System);
        var ledger = new EfLedgerService(dbContext, Options.Create(new LedgerOptions()));
        var adTracking = new EfAdTrackingService(dbContext, TimeProvider.System);
        return new EfPaymentService(dbContext, provider, ledger, adTracking, Options.Create(paymentOptions), TimeProvider.System);
    }

    private static PaymentProviderOptions SignedWebhookOptions() =>
        new()
        {
            WebhookSigningSecret = "test-webhook-secret"
        };

    private static Dictionary<string, string> SignedHeaders(string payload, string secret) =>
        new()
        {
            [FakePaymentProvider.HeaderSignatureKey] = ComputeSignature(payload, secret)
        };

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<(BuyerProfile Buyer, ProductVariant Variant, Order Order)> SeedOrderWithReservationAsync(SwyftlyDbContext dbContext)
    {
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, variant.AvailableQuantity);
        variant.Reserve(2);
        var reservation = new InventoryReservation(variant.Id, buyer.Id, cart.Id, 2, now.AddMinutes(15), now);
        var order = new Order(buyer.Id, product.SellerId, cart.Id, now);
        order.AddItem(product.Id, variant.Id, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2);

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        dbContext.InventoryReservations.Add(reservation);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return (buyer, variant, order);
    }

    private static string PaidPayload(string eventId, string providerReference) =>
        $$"""
        {
          "eventId": "{{eventId}}",
          "eventType": "payment.paid",
          "providerReference": "{{providerReference}}",
          "status": "Paid",
          "occurredAtUtc": "2026-05-18T12:01:00Z"
        }
        """;

    private static string FailedPayload(string eventId, string providerReference) =>
        $$"""
        {
          "eventId": "{{eventId}}",
          "eventType": "payment.failed",
          "providerReference": "{{providerReference}}",
          "status": "Failed",
          "occurredAtUtc": "2026-05-18T12:01:00Z"
        }
        """;

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"PaymentServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SwyftlyDbContext(options);
    }
}
