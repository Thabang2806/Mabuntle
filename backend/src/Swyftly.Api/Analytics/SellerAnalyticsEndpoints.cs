using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Analytics;

public static class SellerAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapSellerAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/seller/analytics/summary", GetSummaryAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsSummary")
            .WithSummary("Returns aggregate seller-owned sales, product, ad, and AI usage metrics.")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly)
            .Produces<SellerAnalyticsSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == seller.Id)
            .ToListAsync(cancellationToken);
        var salesOrders = orders
            .Where(order => IsSalesOrderStatus(order.Status))
            .ToArray();
        var totalSales = salesOrders.Sum(order => order.TotalAmount);
        var orderCount = salesOrders.Length;
        var productsSold = salesOrders.SelectMany(order => order.Items).Sum(item => item.Quantity);
        var totalRefunded = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == seller.Id && refund.Status == RefundStatus.Refunded)
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0m;
        var returnCount = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(returnRequest => returnRequest.SellerId == seller.Id, cancellationToken);
        var refundedOrderCount = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == seller.Id && refund.Status == RefundStatus.Refunded)
            .Select(refund => refund.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);

        var topProducts = salesOrders
            .SelectMany(order => order.Items)
            .GroupBy(item => new { item.ProductId, item.ProductTitle })
            .Select(group => new SellerTopProductResponse(
                group.Key.ProductId,
                group.Key.ProductTitle,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.LineTotal)))
            .OrderByDescending(product => product.QuantitySold)
            .ThenByDescending(product => product.Revenue)
            .Take(5)
            .ToArray();
        var lowStockProducts = await GetLowStockProductsAsync(seller.Id, dbContext, cancellationToken);
        var adPerformance = await GetAdPerformanceAsync(seller.Id, dbContext, cancellationToken);
        var aiUsage = await GetAiUsageAsync(seller.Id, dbContext, cancellationToken);

        return HttpResults.Ok(new SellerAnalyticsSummaryResponse(
            seller.Id,
            totalSales,
            orderCount,
            orderCount == 0 ? 0 : decimal.Round(totalSales / orderCount, 2),
            0,
            productsSold,
            totalRefunded,
            orderCount == 0 ? 0 : decimal.Round((decimal)refundedOrderCount / orderCount, 4),
            orderCount == 0 ? 0 : decimal.Round((decimal)returnCount / orderCount, 4),
            topProducts,
            lowStockProducts,
            adPerformance,
            aiUsage));
    }

    private static async Task<IReadOnlyCollection<SellerLowStockProductResponse>> GetLowStockProductsAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .Select(product => new { product.Id, product.Title, product.Status })
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.Id).ToArray();
        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId))
            .ToListAsync(cancellationToken);

        return products
            .Select(product =>
            {
                var productVariants = variants.Where(variant => variant.ProductId == product.Id).ToArray();
                return new SellerLowStockProductResponse(
                    product.Id,
                    product.Title,
                    product.Status.ToString(),
                    productVariants.Sum(variant => variant.StockQuantity - variant.ReservedQuantity),
                    productVariants.Count(variant => variant.StockQuantity - variant.ReservedQuantity <= 5));
            })
            .Where(product => product.AvailableQuantity <= 5 || product.LowStockVariantCount > 0)
            .OrderBy(product => product.AvailableQuantity)
            .ThenBy(product => product.Title)
            .Take(10)
            .ToArray();
    }

    private static async Task<SellerAdAnalyticsResponse> GetAdPerformanceAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .Select(campaign => campaign.Id)
            .ToListAsync(cancellationToken);
        if (campaignIds.Count == 0)
        {
            return new SellerAdAnalyticsResponse(0, 0, 0, 0, 0, 0, 0, []);
        }

        var campaignSummaries = new List<SellerAdCampaignAnalyticsResponse>();
        foreach (var campaignId in campaignIds)
        {
            var campaign = await dbContext.AdCampaigns.AsNoTracking().SingleAsync(campaign => campaign.Id == campaignId, cancellationToken);
            var impressions = await dbContext.AdImpressions.CountAsync(impression => impression.AdCampaignId == campaignId, cancellationToken);
            var clicks = await dbContext.AdClicks.CountAsync(click => click.AdCampaignId == campaignId, cancellationToken);
            var spend = await dbContext.AdCharges
                .Where(charge => charge.AdCampaignId == campaignId)
                .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
            var conversions = await dbContext.AdConversions
                .AsNoTracking()
                .Where(conversion => conversion.AdCampaignId == campaignId)
                .ToListAsync(cancellationToken);
            var revenue = conversions.Sum(conversion => conversion.RevenueAmount);
            campaignSummaries.Add(new SellerAdCampaignAnalyticsResponse(
                campaign.Id,
                campaign.Name,
                campaign.Status.ToString(),
                impressions,
                clicks,
                impressions == 0 ? 0 : decimal.Round((decimal)clicks / impressions, 4),
                spend,
                conversions.Select(conversion => conversion.OrderId).Distinct().Count(),
                revenue,
                spend == 0 ? 0 : decimal.Round(revenue / spend, 4)));
        }

        var totalImpressions = campaignSummaries.Sum(campaign => campaign.Impressions);
        var totalClicks = campaignSummaries.Sum(campaign => campaign.Clicks);
        var totalSpend = campaignSummaries.Sum(campaign => campaign.Spend);
        var totalRevenue = campaignSummaries.Sum(campaign => campaign.RevenueGenerated);

        return new SellerAdAnalyticsResponse(
            campaignSummaries.Count,
            totalImpressions,
            totalClicks,
            totalImpressions == 0 ? 0 : decimal.Round((decimal)totalClicks / totalImpressions, 4),
            totalSpend,
            campaignSummaries.Sum(campaign => campaign.OrdersGenerated),
            totalRevenue,
            campaignSummaries
                .OrderByDescending(campaign => campaign.RevenueGenerated)
                .ThenByDescending(campaign => campaign.Clicks)
                .Take(5)
                .ToArray());
    }

    private static async Task<SellerAiUsageAnalyticsResponse> GetAiUsageAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var usage = await dbContext.AiUsageLogs
            .AsNoTracking()
            .Where(log => log.SellerId == sellerId)
            .ToListAsync(cancellationToken);
        var suggestions = await dbContext.AiProductSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.SellerId == sellerId)
            .ToListAsync(cancellationToken);
        var appliedSuggestionIds = suggestions
            .Where(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied)
            .Select(suggestion => suggestion.Id)
            .ToArray();
        var fieldAudits = appliedSuggestionIds.Length == 0
            ? []
            : await dbContext.AiSuggestionFieldAudits
                .AsNoTracking()
                .Where(audit => appliedSuggestionIds.Contains(audit.SuggestionId))
                .ToListAsync(cancellationToken);
        var acceptedSuggestions = suggestions.Count(suggestion =>
            suggestion.Status is AiProductSuggestionStatus.Accepted or AiProductSuggestionStatus.Applied);

        return new SellerAiUsageAnalyticsResponse(
            usage.Count,
            usage.Count(log => log.Success),
            usage.Count(log => !log.Success),
            usage.Sum(log => log.CostEstimate ?? 0),
            usage.Count == 0 ? 0 : decimal.Round((decimal)usage.Average(log => log.LatencyMs), 2),
            suggestions.Count,
            acceptedSuggestions,
            suggestions.Count == 0 ? 0 : decimal.Round((decimal)acceptedSuggestions / suggestions.Count, 4),
            suggestions
                .Where(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied)
                .Select(suggestion => suggestion.ProductId)
                .Distinct()
                .Count(),
            suggestions.Count == 0 ? 0 : decimal.Round(suggestions.Average(suggestion => suggestion.QualityScore), 2),
            null,
            "Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.",
            fieldAudits.Count(audit => audit.WasAccepted),
            fieldAudits.Count(audit => audit.WasEdited));
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static bool IsSalesOrderStatus(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.Processing
            or OrderStatus.ReadyToShip
            or OrderStatus.Shipped
            or OrderStatus.Delivered
            or OrderStatus.ReturnRequested
            or OrderStatus.Disputed
            or OrderStatus.Completed;

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerAnalytics.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record SellerAnalyticsSummaryResponse(
    Guid SellerId,
    decimal TotalSales,
    int OrderCount,
    decimal AverageOrderValue,
    decimal ConversionRatePlaceholder,
    int ProductsSold,
    decimal TotalRefunded,
    decimal RefundRate,
    decimal ReturnRate,
    IReadOnlyCollection<SellerTopProductResponse> TopProducts,
    IReadOnlyCollection<SellerLowStockProductResponse> LowStockProducts,
    SellerAdAnalyticsResponse AdPerformance,
    SellerAiUsageAnalyticsResponse AiUsage);

public sealed record SellerTopProductResponse(
    Guid ProductId,
    string? ProductTitle,
    int QuantitySold,
    decimal Revenue);

public sealed record SellerLowStockProductResponse(
    Guid ProductId,
    string? Title,
    string Status,
    int AvailableQuantity,
    int LowStockVariantCount);

public sealed record SellerAdAnalyticsResponse(
    int CampaignCount,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    IReadOnlyCollection<SellerAdCampaignAnalyticsResponse> TopCampaigns);

public sealed record SellerAdCampaignAnalyticsResponse(
    Guid AdCampaignId,
    string Name,
    string Status,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    decimal ReturnOnAdSpend);

public sealed record SellerAiUsageAnalyticsResponse(
    int Requests,
    int SuccessfulRequests,
    int FailedRequests,
    decimal EstimatedCost,
    decimal AverageLatencyMs,
    int SuggestionsGenerated,
    int SuggestionsAccepted,
    decimal SuggestionAcceptanceRate,
    int ProductsImprovedWithAi,
    decimal AverageListingQualityScore,
    decimal? AverageQualityScoreImprovement,
    string QualityScoreImprovementNote,
    int FieldValuesAccepted,
    int FieldValuesEdited);
