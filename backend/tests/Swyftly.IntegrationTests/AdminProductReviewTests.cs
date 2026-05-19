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
using Swyftly.Api.Admin;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AdminProductReviewTests
{
    [Fact]
    public async Task Buyer_CannotAccessAdminProductEndpoints()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/products/pending-review");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListPendingReviewProducts()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/products/pending-review");

        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<AdminProductSummaryResponse[]>();
        Assert.NotNull(products);
        var product = Assert.Single(products!, item => item.ProductId == productId);
        Assert.Equal("PendingReview", product.Status);
    }

    [Fact]
    public async Task Approve_PublishesVerifiedSellerProduct_AndWritesAuditLog()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest());

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Published", detail!.Status);
        Assert.Contains(detail.AuditTrail, entry => entry.ActionType == "ProductApproved");
    }

    [Fact]
    public async Task Reject_RequiresReason_AndWritesAuditLog()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/reject",
            new AdminProductReasonRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/reject",
            new AdminProductReasonRequest("Listing images do not match the product."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Rejected", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductRejected" && entry.Reason == "Listing images do not match the product.");
    }

    [Fact]
    public async Task RequestChanges_RequiresReason_AndMovesProductToChangesRequested()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/request-changes",
            new AdminProductReasonRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/request-changes",
            new AdminProductReasonRequest("Add clearer size measurements."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("ChangesRequested", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductChangesRequested" && entry.Reason == "Add clearer size measurements.");
    }

    [Fact]
    public async Task Approve_HighRiskModerationRequiresOverrideReason()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory, needsAdminReview: true);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var blockedResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest());

        Assert.Equal(HttpStatusCode.BadRequest, blockedResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest("Reviewed supplier documents manually."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Published", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductApproved" && entry.Reason == "Reviewed supplier documents manually.");
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-product-admin@example.test";
        await RegisterAsync(client, email, SwyftlyRoles.Buyer);
        return await LoginAsync(client, email);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminProductReviewTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-products-{Guid.NewGuid():N}@example.test";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(admin, SwyftlyRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
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

    private static async Task<Guid> CreateReviewProductAsync(
        AdminProductReviewTestFactory factory,
        bool needsAdminReview = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Review Seller",
            "review-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Review Seller Trading");

        var storefront = new SellerStorefront(seller.Id, "Review Seller", $"review-seller-{Guid.NewGuid():N}");
        var address = new SellerAddress(
            seller.Id,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            needsAdminReview ? "Designer inspired dress" : "Summer Dress",
            $"admin-review-product-{Guid.NewGuid():N}",
            "A lightweight summer dress.",
            needsAdminReview
                ? "A mirror quality look for evening events."
                : "A lightweight summer dress with a relaxed fit.");
        product.SubmitForReview(
            hasAtLeastOneImage: true,
            hasAtLeastOneActiveVariant: true,
            needsAdminReview);

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        dbContext.Products.Add(product);
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "size", "\"M\""));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "colour", "\"Black\""));
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499.99m,
            699.99m,
            10));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            "https://example.test/summer-dress.jpg",
            $"products/{product.Id:N}/primary.jpg",
            "Summer dress",
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));

        if (needsAdminReview)
        {
            dbContext.AiModerationResults.Add(new AiModerationResult(
                product.Id,
                seller.Id,
                AiModerationRiskLevel.High,
                needsAdminReview: true,
                "Potential counterfeit wording detected.",
                "[\"designer inspired\",\"mirror quality\"]",
                "[]",
                "[\"counterfeit-risk\"]",
                "local-rule-engine",
                DateTimeOffset.UtcNow));
        }

        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private sealed class AdminProductReviewTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAdminProductReviewTests_{Guid.NewGuid():N}";

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
