using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swyftly.Api.Catalog;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class PublicProductSearchTests
{
    [Fact]
    public async Task Search_ReturnsOnlyPublishedProducts()
    {
        using var factory = new PublicProductSearchTestFactory();
        using var client = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory, "Published Seller", "published-seller");
        var publishedProductId = await CreateProductAsync(factory, sellerId, "Published Dress", "published-dress", 499m, ProductSeedStatus.Published);
        await CreateProductAsync(factory, sellerId, "Draft Dress", "draft-dress", 399m, ProductSeedStatus.Draft);

        using var response = await client.GetAsync("/api/products/search?query=dress");

        response.EnsureSuccessStatusCode();
        var search = await response.Content.ReadFromJsonAsync<ProductSearchResponse>();
        Assert.NotNull(search);
        var item = Assert.Single(search!.Items);
        Assert.Equal(publishedProductId, item.ProductId);
        Assert.Equal("Published Dress", item.Title);
    }

    [Fact]
    public async Task Search_FiltersByVariantPriceAndAttributes()
    {
        using var factory = new PublicProductSearchTestFactory();
        using var client = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory, "Filter Seller", "filter-seller");
        var matchingProductId = await CreateProductAsync(
            factory,
            sellerId,
            "Cotton Black Dress",
            "cotton-black-dress",
            499m,
            ProductSeedStatus.Published,
            size: "M",
            colour: "Black",
            material: "Cotton");
        await CreateProductAsync(
            factory,
            sellerId,
            "Silk Red Dress",
            "silk-red-dress",
            999m,
            ProductSeedStatus.Published,
            size: "L",
            colour: "Red",
            material: "Silk");

        using var response = await client.GetAsync(
            "/api/products/search?minPrice=400&maxPrice=600&size=M&colour=Black&material=cotton&inStock=true");

        response.EnsureSuccessStatusCode();
        var search = await response.Content.ReadFromJsonAsync<ProductSearchResponse>();
        Assert.NotNull(search);
        var item = Assert.Single(search!.Items);
        Assert.Equal(matchingProductId, item.ProductId);
    }

    [Fact]
    public async Task Search_PaginatesAndSortsByPrice()
    {
        using var factory = new PublicProductSearchTestFactory();
        using var client = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory, "Price Seller", "price-seller");
        await CreateProductAsync(factory, sellerId, "Low Dress", "low-dress", 199m, ProductSeedStatus.Published);
        var middleProductId = await CreateProductAsync(factory, sellerId, "Middle Dress", "middle-dress", 299m, ProductSeedStatus.Published);
        await CreateProductAsync(factory, sellerId, "High Dress", "high-dress", 399m, ProductSeedStatus.Published);

        using var response = await client.GetAsync("/api/products/search?sort=price_asc&page=2&pageSize=1");

        response.EnsureSuccessStatusCode();
        var search = await response.Content.ReadFromJsonAsync<ProductSearchResponse>();
        Assert.NotNull(search);
        Assert.Equal(3, search!.TotalCount);
        Assert.Equal(2, search.Page);
        var item = Assert.Single(search.Items);
        Assert.Equal(middleProductId, item.ProductId);
    }

    private static async Task<Guid> CreateSellerAsync(
        PublicProductSearchTestFactory factory,
        string storeName,
        string storeSlug)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            storeName,
            $"{storeSlug}@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            $"{storeName} Trading");
        var storefront = new SellerStorefront(seller.Id, storeName, storeSlug);
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);
        storefront.Publish();

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        await dbContext.SaveChangesAsync();

        return seller.Id;
    }

    private static async Task<Guid> CreateProductAsync(
        PublicProductSearchTestFactory factory,
        Guid sellerId,
        string title,
        string slug,
        decimal price,
        ProductSeedStatus status,
        string size = "M",
        string colour = "Black",
        string material = "Cotton")
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            title,
            slug,
            "A marketplace-ready dress.",
            $"A {material.ToLowerInvariant()} dress for public search testing.");
        product.UpdateTags("[\"dress\",\"search\"]");

        if (status == ProductSeedStatus.Published)
        {
            product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
            product.Publish(DateTimeOffset.UtcNow);
        }

        dbContext.Products.Add(product);
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "size", $"\"{size}\""));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "colour", $"\"{colour}\""));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", $"\"{material}\""));
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            size,
            colour,
            price,
            price + 100,
            stockQuantity: 10));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            $"https://example.test/{slug}.jpg",
            $"products/{product.Id:N}/primary.jpg",
            title,
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private enum ProductSeedStatus
    {
        Draft,
        Published
    }

    private sealed class PublicProductSearchTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyPublicProductSearchTests_{Guid.NewGuid():N}";

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
