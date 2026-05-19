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
using Swyftly.Api.Ai;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class BuyerAiShoppingAssistantTests
{
    [Fact]
    public async Task Seller_CannotUseBuyerShoppingAssistant()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var sellerToken = await RegisterAndLoginAsync(client, SwyftlyRoles.Seller);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("black dress"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BuyerShoppingAssistant_ReturnsOnlyRealPublishedProductIdsFromSearchResults()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, SwyftlyRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));

        response.EnsureSuccessStatusCode();
        var assistant = await response.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(assistant);
        var product = Assert.Single(assistant!.Products);
        Assert.Equal(productId, product.ProductId);
        Assert.Contains(product.MatchReasons, reason => reason.Contains("Black", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Dresses", assistant.Intent.Category);
        Assert.Equal("M", assistant.Intent.Size);
        Assert.Equal(1500m, assistant.Intent.BudgetMax);
    }

    [Fact]
    public async Task BuyerShoppingAssistant_ReturnsEmptyResultsWithoutInventingProducts()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        _ = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, SwyftlyRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Find gold earrings for sensitive ears under R300."));

        response.EnsureSuccessStatusCode();
        var assistant = await response.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(assistant);
        Assert.Empty(assistant!.Products);
        Assert.Contains("No exact products matched", assistant.Summary);
    }

    private static async Task<Guid> SeedPublishedDressAsync(BuyerAiShoppingAssistantTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Assistant Seller",
            "assistant-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Assistant Seller Trading");
        var category = new Category(Guid.NewGuid(), null, "Dresses", $"assistant-dresses-{Guid.NewGuid():N}");
        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            category.Id,
            null,
            "Black Wedding Dress",
            $"black-wedding-dress-{Guid.NewGuid():N}",
            "A black dress for formal occasions.",
            "A black dress suitable for weddings and evening events.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.AddRange(
            seller,
            category,
            product,
            new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", 999m, null, 5),
            new ProductImage(
                product.Id,
                "https://example.test/black-dress.jpg",
                $"products/{product.Id:N}/primary.jpg",
                "Black wedding dress",
                0,
                isPrimary: true,
                DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}-assistant-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class BuyerAiShoppingAssistantTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyBuyerAiShoppingAssistantTests_{Guid.NewGuid():N}";

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
