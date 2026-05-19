using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swyftly.Api.Authentication;
using Swyftly.Api.Sellers;
using Swyftly.Application.Identity;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class SellerProductDraftTests
{
    [Fact]
    public async Task Seller_CanCreateDraftAddVariantAndImage()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "product-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);
        var withImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        Assert.Equal("Draft", product.Status);
        Assert.Single(withVariant.Variants);
        Assert.Single(withImage.Images);
        Assert.True(withImage.Images.Single().IsPrimary);
    }

    [Fact]
    public async Task Seller_CannotAccessAnotherSellersProduct()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "seller-one-products@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "seller-two-products@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.GetAsync($"/api/seller/products/{product.ProductId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Product_CannotHaveTwoPrimaryImages()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "primary-image-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images",
            new AttachSellerProductImageRequest(
                "second-image",
                "https://example.test/second.jpg",
                "Second image",
                1,
                IsPrimary: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnverifiedSeller_CannotSubmitProductForReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "unverified-product-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifiedSeller_CanSubmitCompleteProductForReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "verified-product-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("PendingReview", submitted!.Status);
    }

    [Fact]
    public async Task SubmitReview_WithCounterfeitRiskTerms_StoresModerationFlagsAndNeedsAdminReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "counterfeit-risk-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(
            client,
            title: "Designer inspired summer dress",
            fullDescription: "A mirror quality look for evening events.");
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("NeedsAdminReview", submitted!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var moderation = await dbContext.AiModerationResults.SingleAsync(result => result.ProductId == product.ProductId);

        Assert.True(moderation.NeedsAdminReview);
        Assert.Equal(AiModerationRiskLevel.High, moderation.RiskLevel);
        Assert.Contains("designer inspired", moderation.DetectedTermsJson);
        Assert.Contains("mirror quality", moderation.DetectedTermsJson);
    }

    [Fact]
    public async Task SubmitReview_ForBeautyProductMissingSafetyFields_StoresModerationFlags()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "beauty-missing-fields-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(
            client,
            categoryId: CatalogSeedData.BeautyFoundation,
            title: "Matte foundation",
            fullDescription: "A lightweight foundation for daily wear.",
            attributes: new Dictionary<string, object?>
            {
                ["shade"] = "Medium"
            });
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("NeedsAdminReview", submitted!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var moderation = await dbContext.AiModerationResults.SingleAsync(result => result.ProductId == product.ProductId);

        Assert.Contains("ingredients", moderation.MissingFieldsJson);
        Assert.Contains("expiry date", moderation.MissingFieldsJson);
        Assert.Contains("batch number", moderation.MissingFieldsJson);
        Assert.Contains("sealed/unsealed status", moderation.MissingFieldsJson);
    }

    [Fact]
    public async Task AiSuggestion_RejectsUnauthenticatedAccess()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{Guid.NewGuid()}/ai-suggestions",
            CreateAiSuggestionRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AiSuggestion_RejectsWrongSeller()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "ai-seller-one@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "ai-seller-two@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AiSuggestion_GeneratesAndPersistsSuggestionWithFakeProvider()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-generation-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var productWithImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest(productWithImage.Images.Select(image => image.ImageId).ToArray()));

        response.EnsureSuccessStatusCode();
        var suggestion = await response.Content.ReadFromJsonAsync<SellerAiSuggestionResponse>();
        Assert.NotNull(suggestion);
        Assert.Equal("AI-assisted product title", suggestion!.RecommendedTitle);
        Assert.Contains("brand", suggestion.MissingFields);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var savedSuggestion = await dbContext.AiProductSuggestions.SingleAsync();
        var usageLog = await dbContext.AiUsageLogs.SingleAsync();
        var sellerProfile = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == seller.UserId);

        Assert.Equal(product.ProductId, savedSuggestion.ProductId);
        Assert.Equal(sellerProfile.Id, savedSuggestion.SellerId);
        Assert.True(usageLog.Success);
        Assert.Equal(seller.UserId.ToString(), usageLog.UserId);
    }

    [Fact]
    public async Task AiSuggestionApply_CanApplyPartialEditedValuesAndWritesAudits()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-apply-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var productWithImage = await AddImageAsync(client, product.ProductId, isPrimary: true);
        var imageId = productWithImage.Images.Single().ImageId;

        using var generateResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest([imageId]));
        generateResponse.EnsureSuccessStatusCode();
        var suggestion = await generateResponse.Content.ReadFromJsonAsync<SellerAiSuggestionResponse>();
        Assert.NotNull(suggestion);

        using var applyResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions/{suggestion!.SuggestionId}/apply",
            new
            {
                fieldsToApply = new[] { "title", "tags", "imageAltText" },
                editedValues = new
                {
                    title = "Seller reviewed AI title",
                    tags = new[] { "summer", "reviewed" },
                    imageAltText = new Dictionary<string, string?>
                    {
                        [imageId.ToString()] = "Model wearing a black summer dress"
                    }
                }
            });

        applyResponse.EnsureSuccessStatusCode();
        var updatedProduct = await applyResponse.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(updatedProduct);
        Assert.Equal("Seller reviewed AI title", updatedProduct!.Title);
        Assert.Contains("summer", updatedProduct.Tags);
        Assert.Equal("Model wearing a black summer dress", updatedProduct.Images.Single().AltText);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var savedSuggestion = await dbContext.AiProductSuggestions.SingleAsync(item => item.Id == suggestion.SuggestionId);
        var audits = await dbContext.AiSuggestionFieldAudits
            .Where(audit => audit.SuggestionId == suggestion.SuggestionId)
            .ToListAsync();

        Assert.Equal("Applied", savedSuggestion.Status.ToString());
        Assert.Equal(3, audits.Count);
        Assert.Contains(audits, audit => audit.FieldName == "title" && audit.WasEdited);
        Assert.Contains(audits, audit => audit.FieldName == "tags" && audit.WasEdited);
        Assert.Contains(audits, audit => audit.FieldName == "imageAltText" && audit.WasEdited);
    }

    [Fact]
    public async Task AiSuggestionApply_RejectsInvalidAttributeValues()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-invalid-attribute-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var suggestionId = await CreateInvalidAttributeSuggestionAsync(factory, product.ProductId, seller.UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions/{suggestionId}/apply",
            new
            {
                fieldsToApply = new[] { "attributes" },
                editedValues = new { }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Empty(await dbContext.AiSuggestionFieldAudits.Where(audit => audit.SuggestionId == suggestionId).ToListAsync());
    }

    private static async Task<AuthResponse> RegisterAndLoginSellerAsync(HttpClient client, string email)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Seller));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!;
    }

    private static async Task<SellerProductDetailResponse> CreateProductAsync(
        HttpClient client,
        Guid? categoryId = null,
        string title = "Summer Dress",
        string fullDescription = "A lightweight summer dress with a relaxed fit.",
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/seller/products",
            new
            {
                categoryId = categoryId ?? CatalogSeedData.WomenDresses,
                brandId = (Guid?)null,
                title,
                slug = $"product-{Guid.NewGuid():N}",
                shortDescription = "A lightweight summer dress.",
                fullDescription,
                attributes = attributes ?? new Dictionary<string, object?>
                {
                    ["size"] = "M",
                    ["colour"] = "Black"
                }
            });
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<SellerProductDetailResponse> AddVariantAsync(HttpClient client, Guid productId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{productId}/variants",
            new UpsertSellerProductVariantRequest(
                "SUMMER-DRESS-M-BLACK",
                "M",
                "Black",
                499.99m,
                699.99m,
                10,
                0,
                "Active",
                null));
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<SellerProductDetailResponse> AddImageAsync(
        HttpClient client,
        Guid productId,
        bool isPrimary)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{productId}/images",
            new AttachSellerProductImageRequest(
                "summer-dress-primary",
                "https://example.test/summer-dress.jpg",
                "Summer dress",
                0,
                isPrimary));
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task MarkSellerVerifiedAsync(
        SellerProductDraftTestFactory factory,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == userId);

        seller.UpdateProfile(
            "Verified Seller",
            "verified-product-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Verified Trading");

        var storefront = new SellerStorefront(seller.Id, "Verified Seller", $"verified-{seller.Id:N}");
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

        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        seller.MarkVerified(storefront, address, payout);

        await dbContext.SaveChangesAsync();
    }

    private static GenerateSellerAiSuggestionRequest CreateAiSuggestionRequest(
        IReadOnlyCollection<Guid>? imageIds = null) =>
        new(
            "Lightweight summer dress; brand is not confirmed.",
            "Dress",
            CatalogSeedData.WomenDresses,
            new Dictionary<string, JsonElement>(),
            imageIds ?? []);

    private static async Task<Guid> CreateInvalidAttributeSuggestionAsync(
        SellerProductDraftTestFactory factory,
        Guid productId,
        Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == sellerUserId);
        var suggestion = new AiProductSuggestion(
            seller.Id,
            productId,
            "Invalid size suggestion",
            "[]",
            "Suggested title",
            "Suggested short",
            "Suggested full",
            CatalogSeedData.WomenDresses,
            "Women > Clothing > Dresses",
            "{\"size\":\"XXL\"}",
            "[]",
            "[]",
            "[]",
            50,
            "local-test-model",
            "listing-assistant-v1",
            DateTimeOffset.UtcNow);

        dbContext.AiProductSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();
        return suggestion.Id;
    }

    private sealed class SellerProductDraftTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlySellerProductDraftTests_{Guid.NewGuid():N}";

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
