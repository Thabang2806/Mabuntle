using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Search;
using Swyftly.Domain.Catalog;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Search;

namespace Swyftly.UnitTests.Infrastructure;

public class ProductSearchIndexerTests
{
    [Fact]
    public async Task ProductSearchIndexer_BuildsAndIndexesPublishedProductDocument()
    {
        await using var dbContext = CreateDbContext();
        var searchIndex = new LocalSearchIndexService();
        var product = new Product(Guid.NewGuid());
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            "Cotton Summer Dress",
            "cotton-summer-dress",
            "A cotton summer dress.",
            "A breathable dress for warm weather.");
        product.UpdateTags("[\"summer\",\"cotton\"]");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.Categories.AddRange(CatalogSeedData.CreateCategories());
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            "SKU-1",
            "M",
            "Black",
            499m,
            699m,
            10));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
        await dbContext.SaveChangesAsync();

        var indexer = new ProductSearchIndexer(dbContext, searchIndex);

        await indexer.IndexProductAsync(product.Id);
        var result = await searchIndex.SearchProductsAsync(new ProductSearchIndexQuery(
            "cotton",
            CatalogSeedData.WomenDresses,
            "Women > Clothing > Dresses",
            product.SellerId,
            400m,
            600m,
            "M",
            "Black",
            null,
            "Cotton",
            true,
            "newest",
            1,
            24));

        Assert.NotNull(result);
        Assert.Equal("local-memory", result!.ProviderName);
        Assert.Equal(product.Id, Assert.Single(result.ProductIds));
    }

    [Fact]
    public async Task LocalSearchIndex_ReturnsNullWhenNoDocumentsAreIndexed()
    {
        var searchIndex = new LocalSearchIndexService();

        var result = await searchIndex.SearchProductsAsync(new ProductSearchIndexQuery(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "newest",
            1,
            24));

        Assert.Null(result);
    }

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"ProductSearchIndexerTests-{Guid.NewGuid():N}")
            .Options;

        return new SwyftlyDbContext(options);
    }
}
