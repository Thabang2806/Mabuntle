using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swyftly.Api.Analytics;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Returns;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class SellerAnalyticsTests
{
    [Fact]
    public async Task Buyer_CannotAccessSellerAnalytics()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "analytics-buyer@example.test", SwyftlyRoles.Buyer);
        var token = await LoginAsync(client, "analytics-buyer@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/seller/analytics/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SellerAnalytics_ReturnsOnlyAuthenticatedSellerMetrics()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await CreateSellerUserAsync(factory, client, "analytics-seller-one@example.test");
        var sellerTwo = await CreateSellerUserAsync(factory, client, "analytics-seller-two@example.test");
        await SeedAnalyticsDataAsync(factory, sellerOne.SellerId, sellerTwo.SellerId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);

        using var response = await client.GetAsync("/api/seller/analytics/summary");

        response.EnsureSuccessStatusCode();
        var analytics = await response.Content.ReadFromJsonAsync<SellerAnalyticsSummaryResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(sellerOne.SellerId, analytics!.SellerId);
        Assert.Equal(998m, analytics.TotalSales);
        Assert.Equal(1, analytics.OrderCount);
        Assert.Equal(2, analytics.ProductsSold);
        Assert.Equal(100m, analytics.TotalRefunded);
        Assert.Equal(1m, analytics.RefundRate);
        Assert.Equal(1m, analytics.ReturnRate);
        Assert.DoesNotContain(analytics.TopProducts, product => product.ProductTitle == "Other Seller Product");
        Assert.Contains(analytics.TopProducts, product => product.ProductTitle == "Seller One Product");
        Assert.Contains(analytics.LowStockProducts, product => product.Title == "Seller One Product");
        Assert.Equal(1, analytics.AdPerformance.CampaignCount);
        Assert.Equal(1, analytics.AdPerformance.Impressions);
        Assert.Equal(1, analytics.AdPerformance.Clicks);
        Assert.Equal(1, analytics.AdPerformance.OrdersGenerated);
        Assert.Equal(3, analytics.AiUsage.Requests);
        Assert.Equal(2, analytics.AiUsage.SuccessfulRequests);
        Assert.Equal(1, analytics.AiUsage.FailedRequests);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<(Guid SellerId, string AccessToken)> CreateSellerUserAsync(
        SellerAnalyticsTestFactory factory,
        HttpClient client,
        string email)
    {
        await RegisterAsync(client, email, SwyftlyRoles.Seller);
        var token = await LoginAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var sellerId = await dbContext.SellerProfiles
            .Where(seller => seller.UserId == user!.Id)
            .Select(seller => seller.Id)
            .SingleAsync();

        return (sellerId, token);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task SeedAnalyticsDataAsync(
        SellerAnalyticsTestFactory factory,
        Guid sellerOneId,
        Guid sellerTwoId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var buyerId = Guid.NewGuid();
        var sellerOneProduct = CreatePublishedProduct(sellerOneId, "Seller One Product");
        var sellerTwoProduct = CreatePublishedProduct(sellerTwoId, "Other Seller Product");
        var sellerOneVariant = new ProductVariant(sellerOneProduct.Id, "SKU-1", "M", "Black", 499m, null, 3);
        var sellerTwoVariant = new ProductVariant(sellerTwoProduct.Id, "SKU-2", "M", "Black", 999m, null, 10);

        var sellerOneOrder = new Order(buyerId, sellerOneId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        sellerOneOrder.AddItem(sellerOneProduct.Id, sellerOneVariant.Id, sellerOneProduct.Title, sellerOneVariant.Sku, sellerOneVariant.Size, sellerOneVariant.Colour, sellerOneVariant.Price, 2);
        sellerOneOrder.ChangeStatus(OrderStatus.Paid, DateTimeOffset.UtcNow.AddDays(-1), "PaymentConfirmed");
        var sellerTwoOrder = new Order(Guid.NewGuid(), sellerTwoId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        sellerTwoOrder.AddItem(sellerTwoProduct.Id, sellerTwoVariant.Id, sellerTwoProduct.Title, sellerTwoVariant.Sku, sellerTwoVariant.Size, sellerTwoVariant.Colour, sellerTwoVariant.Price, 1);
        sellerTwoOrder.ChangeStatus(OrderStatus.Paid, DateTimeOffset.UtcNow.AddDays(-1), "PaymentConfirmed");

        var returnRequest = new ReturnRequest(
            sellerOneOrder.Id,
            buyerId,
            sellerOneId,
            ReturnReason.DamagedItem,
            "Damaged on arrival.",
            DateTimeOffset.UtcNow);
        returnRequest.MarkAwaitingSellerResponse(DateTimeOffset.UtcNow);
        var refund = new Refund(sellerOneOrder.Id, Guid.NewGuid(), buyerId, sellerOneId, returnRequest.Id, 100m, "ZAR", "Return approved.", DateTimeOffset.UtcNow);
        refund.Approve(Guid.NewGuid(), "Approved.", DateTimeOffset.UtcNow);
        refund.MarkProcessing(DateTimeOffset.UtcNow);
        refund.MarkRefunded("fake_refund", DateTimeOffset.UtcNow);

        var campaign = new AdCampaign(
            sellerOneId,
            "Launch campaign",
            AdCampaignType.FeaturedProduct,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow.AddDays(-1));
        campaign.ReplaceProducts([sellerOneProduct.Id], DateTimeOffset.UtcNow.AddDays(-1));
        campaign.SubmitForReview(DateTimeOffset.UtcNow.AddDays(-1));
        campaign.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));
        var click = new AdClick(campaign.Id, sellerOneProduct.Id, buyerId, null, DateTimeOffset.UtcNow);

        dbContext.Products.AddRange(sellerOneProduct, sellerTwoProduct);
        dbContext.ProductVariants.AddRange(sellerOneVariant, sellerTwoVariant);
        dbContext.Orders.AddRange(sellerOneOrder, sellerTwoOrder);
        dbContext.ReturnRequests.Add(returnRequest);
        dbContext.Refunds.Add(refund);
        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(new AdBudget(campaign.Id, "ZAR", 100m, 1000m, 5m, DateTimeOffset.UtcNow));
        dbContext.AdImpressions.Add(new AdImpression(campaign.Id, sellerOneProduct.Id, "shop-grid", "visitor-1", DateTimeOffset.UtcNow));
        dbContext.AdClicks.Add(click);
        dbContext.AdCharges.Add(new AdCharge(campaign.Id, click.Id, 5m, "ZAR", "Click", DateTimeOffset.UtcNow));
        dbContext.AdConversions.Add(new AdConversion(campaign.Id, click.Id, sellerOneOrder.Id, sellerOneOrder.TotalAmount, "ZAR", DateTimeOffset.UtcNow));
        dbContext.AiUsageLogs.AddRange(
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 20, 0.01m, 100, true, null, DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 20, 0.01m, 110, true, null, DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 0, 0.005m, 50, false, "Invalid response.", DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "other-user", sellerTwoId, "fake-model", 10, 20, 0.01m, 100, true, null, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static Product CreatePublishedProduct(Guid sellerId, string title)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(Guid.NewGuid(), null, title, $"{title.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}", "Short description.", "Full product description.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow.AddDays(-3));
        return product;
    }

    private sealed class SellerAnalyticsTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlySellerAnalyticsTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }
}
