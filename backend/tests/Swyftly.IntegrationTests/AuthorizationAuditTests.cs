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
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AuthorizationAuditTests
{
    [Fact]
    public async Task Anonymous_CannotAccessSellerProducts()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/seller/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_CannotAccessSellerProducts()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();
        var buyer = await RegisterLoginAndGetProfileAsync<BuyerProfile>(factory, client, SwyftlyRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyer.Token);
        using var response = await client.GetAsync("/api/seller/products");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotAccessAdminReports()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.Token);
        using var response = await client.GetAsync("/api/admin/reports/marketplace");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotReadAnotherSellersProduct()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);
        var sellerTwo = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);
        var productId = await SeedProductAsync(factory, sellerOne.Profile.Id);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.Token);
        using var response = await client.GetAsync($"/api/seller/products/{productId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_CannotReadAnotherBuyersOrder()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();
        var buyerOne = await RegisterLoginAndGetProfileAsync<BuyerProfile>(factory, client, SwyftlyRoles.Buyer);
        var buyerTwo = await RegisterLoginAndGetProfileAsync<BuyerProfile>(factory, client, SwyftlyRoles.Buyer);
        var seller = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);
        var orderId = await SeedOrderAsync(factory, buyerOne.Profile.Id, seller.Profile.Id);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerTwo.Token);
        using var response = await client.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotReadAnotherSellersOrder()
    {
        using var factory = new AuthorizationAuditTestFactory();
        using var client = factory.CreateClient();
        var buyer = await RegisterLoginAndGetProfileAsync<BuyerProfile>(factory, client, SwyftlyRoles.Buyer);
        var sellerOne = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);
        var sellerTwo = await RegisterLoginAndGetProfileAsync<SellerProfile>(factory, client, SwyftlyRoles.Seller);
        var orderId = await SeedOrderAsync(factory, buyer.Profile.Id, sellerOne.Profile.Id);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.Token);
        using var response = await client.GetAsync($"/api/seller/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<AuthenticatedProfile<TProfile>> RegisterLoginAndGetProfileAsync<TProfile>(
        AuthorizationAuditTestFactory factory,
        HttpClient client,
        string role)
        where TProfile : class
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
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

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        object? profile = typeof(TProfile) == typeof(BuyerProfile)
            ? await dbContext.BuyerProfiles.SingleAsync(item => item.UserId == user!.Id)
            : await dbContext.SellerProfiles.SingleAsync(item => item.UserId == user!.Id);

        return new AuthenticatedProfile<TProfile>((TProfile)profile, auth!.AccessToken);
    }

    private static async Task<Guid> SeedProductAsync(AuthorizationAuditTestFactory factory, Guid sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            null,
            null,
            "Authorization Audit Product",
            $"authorization-audit-product-{Guid.NewGuid():N}",
            "Authorization audit short description.",
            "Authorization audit full description.");

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<Guid> SeedOrderAsync(
        AuthorizationAuditTestFactory factory,
        Guid buyerId,
        Guid sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var product = new Product(sellerId);
        var variant = new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", 250m, null, 5);
        var cart = new Cart(buyerId);
        var order = new Order(buyerId, sellerId, cart.Id, DateTimeOffset.UtcNow);
        order.AddItem(product.Id, variant.Id, "Authorization Audit Order Product", variant.Sku, variant.Size, variant.Colour, variant.Price, 1);

        dbContext.AddRange(product, variant, cart, order);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private sealed record AuthenticatedProfile<TProfile>(TProfile Profile, string Token);

    private sealed class AuthorizationAuditTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAuthorizationAuditTests_{Guid.NewGuid():N}";

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
