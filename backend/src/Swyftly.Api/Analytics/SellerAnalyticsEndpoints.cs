using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Disputes;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Returns;
using Swyftly.Domain.Sellers;
using Swyftly.Domain.Support;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Analytics;

public static class SellerAnalyticsEndpoints
{
    private const string DefaultCurrency = "ZAR";
    private const int MaxRangeDays = 366;

    public static IEndpointRouteBuilder MapSellerAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/seller/analytics/summary", GetSummaryAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsSummary")
            .WithSummary("Returns aggregate seller-owned sales, product, ad, and AI usage metrics.")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly)
            .Produces<SellerAnalyticsSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/seller/analytics/performance", GetPerformanceAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsPerformance")
            .WithSummary("Returns seller-owned trend, product, inventory, ad, and customer-care analytics for a date range.")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly)
            .Produces<SellerAnalyticsPerformanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/seller/analytics/export.csv", ExportCsvAsync)
            .WithTags("Seller Analytics")
            .WithName("ExportSellerAnalyticsCsv")
            .WithSummary("Exports seller-owned analytics as CSV.")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly)
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status400BadRequest)
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

    private static async Task<IResult> GetPerformanceAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        if (!TryResolveBucket(bucket, out var bucketKind))
        {
            return InvalidBucketProblem();
        }

        var report = await BuildPerformanceAsync(seller.Id, range.FromUtc, range.ToUtc, bucketKind, dbContext, cancellationToken);
        return HttpResults.Ok(report);
    }

    private static async Task<IResult> ExportCsvAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? report,
        ClaimsPrincipal principal,
        HttpResponse response,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        if (!TryResolveBucket(bucket, out var bucketKind))
        {
            return InvalidBucketProblem();
        }

        if (!TryResolveReport(report, out var reportKind))
        {
            return InvalidReportProblem();
        }

        var analytics = await BuildPerformanceAsync(seller.Id, range.FromUtc, range.ToUtc, bucketKind, dbContext, cancellationToken);
        response.Headers.ContentDisposition = $"attachment; filename=\"swyftly-seller-analytics-{reportKind.ToString().ToLowerInvariant()}.csv\"";

        return HttpResults.Text(BuildCsv(analytics, reportKind), "text/csv", Encoding.UTF8);
    }

    private static async Task<SellerAnalyticsPerformanceResponse> BuildPerformanceAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AnalyticsBucketKind bucket,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == sellerId
                && order.CreatedAtUtc >= fromUtc
                && order.CreatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var salesOrders = orders
            .Where(order => IsSalesOrderStatus(order.Status))
            .ToArray();

        var refunds = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == sellerId
                && refund.Status == RefundStatus.Refunded
                && refund.RefundedAtUtc >= fromUtc
                && refund.RefundedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var returns = await dbContext.ReturnRequests
            .AsNoTracking()
            .Include(returnRequest => returnRequest.Items)
            .Where(returnRequest => returnRequest.SellerId == sellerId
                && returnRequest.RequestedAtUtc >= fromUtc
                && returnRequest.RequestedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .Select(product => new ProductAnalyticsProjection(
                product.Id,
                product.Title,
                product.Slug,
                product.Status))
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.ProductId).ToArray();
        var variants = productIds.Length == 0
            ? []
            : await dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId))
                .Select(variant => new VariantAnalyticsProjection(
                    variant.Id,
                    variant.ProductId,
                    variant.Sku,
                    variant.Barcode,
                    variant.Size,
                    variant.Colour,
                    variant.Status,
                    variant.StockQuantity,
                    variant.ReservedQuantity,
                    variant.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        var movements = productIds.Length == 0
            ? []
            : await dbContext.InventoryMovements
                .AsNoTracking()
                .Where(movement => movement.SellerId == sellerId)
                .GroupBy(movement => movement.ProductVariantId)
                .Select(group => new InventoryMovementLastActivityProjection(
                    group.Key,
                    group.Max(movement => movement.OccurredAtUtc)))
                .ToListAsync(cancellationToken);

        var salesTrend = BuildSalesTrend(fromUtc, toUtc, bucket, salesOrders, refunds);
        var productPerformance = BuildProductPerformance(products, variants, salesOrders, refunds, returns);
        var inventoryPerformance = BuildInventoryPerformance(products, variants, movements);
        var adPerformance = await BuildAdPerformanceAsync(sellerId, fromUtc, toUtc, dbContext, cancellationToken);
        var customerCareSummary = await BuildCustomerCareSummaryAsync(
            sellerId,
            fromUtc,
            toUtc,
            refunds,
            returns,
            dbContext,
            cancellationToken);

        return new SellerAnalyticsPerformanceResponse(
            sellerId,
            fromUtc,
            toUtc,
            bucket.ToString(),
            salesTrend,
            productPerformance,
            inventoryPerformance,
            adPerformance,
            customerCareSummary);
    }

    private static IReadOnlyCollection<SellerSalesTrendBucketResponse> BuildSalesTrend(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AnalyticsBucketKind bucket,
        IReadOnlyCollection<Order> salesOrders,
        IReadOnlyCollection<Refund> refunds)
    {
        var buckets = BuildBuckets(fromUtc, toUtc, bucket);

        return buckets
            .Select(currentBucket =>
            {
                var bucketOrders = salesOrders
                    .Where(order => IsInBucket(order.CreatedAtUtc, currentBucket.PeriodStartUtc, currentBucket.PeriodEndUtc, toUtc))
                    .ToArray();
                var bucketRefunds = refunds
                    .Where(refund => refund.RefundedAtUtc.HasValue
                        && IsInBucket(refund.RefundedAtUtc.Value, currentBucket.PeriodStartUtc, currentBucket.PeriodEndUtc, toUtc))
                    .ToArray();
                var grossSales = bucketOrders.Sum(order => order.TotalAmount);
                var refundedAmount = bucketRefunds.Sum(refund => refund.Amount);

                return new SellerSalesTrendBucketResponse(
                    currentBucket.PeriodStartUtc,
                    currentBucket.PeriodEndUtc,
                    bucketOrders.Length,
                    grossSales,
                    refundedAmount,
                    grossSales - refundedAmount,
                    bucketOrders.SelectMany(order => order.Items).Sum(item => item.Quantity));
            })
            .ToArray();
    }

    private static IReadOnlyCollection<SellerProductPerformanceResponse> BuildProductPerformance(
        IReadOnlyCollection<ProductAnalyticsProjection> products,
        IReadOnlyCollection<VariantAnalyticsProjection> variants,
        IReadOnlyCollection<Order> salesOrders,
        IReadOnlyCollection<Refund> refunds,
        IReadOnlyCollection<ReturnRequest> returns)
    {
        var orderRefunds = refunds
            .GroupBy(refund => refund.OrderId)
            .ToDictionary(group => group.Key, group => group.Sum(refund => refund.Amount));

        return products
            .Select(product =>
            {
                var orderItems = salesOrders
                    .SelectMany(order => order.Items.Select(item => new { Order = order, Item = item }))
                    .Where(pair => pair.Item.ProductId == product.ProductId)
                    .ToArray();
                var productVariants = variants.Where(variant => variant.ProductId == product.ProductId).ToArray();
                var returnedQuantity = returns
                    .SelectMany(returnRequest => returnRequest.Items)
                    .Where(item => item.ProductId == product.ProductId)
                    .Sum(item => item.Quantity);
                var unitsSold = orderItems.Sum(pair => pair.Item.Quantity);
                var refundedAmount = orderItems.Sum(pair =>
                {
                    if (!orderRefunds.TryGetValue(pair.Order.Id, out var orderRefundedAmount) || pair.Order.ItemsSubtotal <= 0)
                    {
                        return 0m;
                    }

                    return decimal.Round(orderRefundedAmount * (pair.Item.LineTotal / pair.Order.ItemsSubtotal), 2);
                });

                return new SellerProductPerformanceResponse(
                    product.ProductId,
                    product.Title,
                    product.Slug,
                    product.Status.ToString(),
                    unitsSold,
                    orderItems.Sum(pair => pair.Item.LineTotal),
                    refundedAmount,
                    returnedQuantity,
                    unitsSold == 0 ? 0 : decimal.Round((decimal)returnedQuantity / unitsSold, 4),
                    productVariants.Sum(variant => variant.StockQuantity),
                    productVariants.Sum(variant => variant.ReservedQuantity),
                    productVariants.Sum(variant => variant.StockQuantity - variant.ReservedQuantity));
            })
            .OrderByDescending(product => product.GrossSales)
            .ThenByDescending(product => product.UnitsSold)
            .ThenBy(product => product.ProductTitle)
            .ToArray();
    }

    private static IReadOnlyCollection<SellerInventoryPerformanceResponse> BuildInventoryPerformance(
        IReadOnlyCollection<ProductAnalyticsProjection> products,
        IReadOnlyCollection<VariantAnalyticsProjection> variants,
        IReadOnlyCollection<InventoryMovementLastActivityProjection> movements)
    {
        var productsById = products.ToDictionary(product => product.ProductId);
        var movementsByVariant = movements.ToDictionary(movement => movement.ProductVariantId, movement => movement.LastMovementAtUtc);

        return variants
            .Select(variant =>
            {
                productsById.TryGetValue(variant.ProductId, out var product);
                var available = variant.StockQuantity - variant.ReservedQuantity;

                return new SellerInventoryPerformanceResponse(
                    variant.ProductId,
                    product?.Title,
                    variant.ProductVariantId,
                    variant.Sku,
                    variant.Barcode,
                    variant.Size,
                    variant.Colour,
                    variant.Status.ToString(),
                    variant.StockQuantity,
                    variant.ReservedQuantity,
                    available,
                    available > 0 && available <= 5,
                    available <= 0 || variant.Status == ProductVariantStatus.OutOfStock,
                    movementsByVariant.GetValueOrDefault(variant.ProductVariantId, variant.UpdatedAtUtc));
            })
            .OrderBy(inventory => inventory.AvailableQuantity)
            .ThenBy(inventory => inventory.ProductTitle)
            .ThenBy(inventory => inventory.Sku)
            .ToArray();
    }

    private static async Task<IReadOnlyCollection<SellerAdPerformanceDetailResponse>> BuildAdPerformanceAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaigns = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.Name,
                campaign.Status
            })
            .ToListAsync(cancellationToken);
        var campaignIds = campaigns.Select(campaign => campaign.Id).ToArray();
        if (campaignIds.Length == 0)
        {
            return [];
        }

        var impressions = await dbContext.AdImpressions
            .AsNoTracking()
            .Where(impression => campaignIds.Contains(impression.AdCampaignId)
                && impression.OccurredAtUtc >= fromUtc
                && impression.OccurredAtUtc <= toUtc)
            .GroupBy(impression => impression.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Count, cancellationToken);
        var clicks = await dbContext.AdClicks
            .AsNoTracking()
            .Where(click => campaignIds.Contains(click.AdCampaignId)
                && click.OccurredAtUtc >= fromUtc
                && click.OccurredAtUtc <= toUtc)
            .GroupBy(click => click.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Count, cancellationToken);
        var spend = await dbContext.AdCharges
            .AsNoTracking()
            .Where(charge => campaignIds.Contains(charge.AdCampaignId)
                && charge.ChargedAtUtc >= fromUtc
                && charge.ChargedAtUtc <= toUtc)
            .GroupBy(charge => charge.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Amount = group.Sum(charge => charge.Amount) })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Amount, cancellationToken);
        var conversions = await dbContext.AdConversions
            .AsNoTracking()
            .Where(conversion => campaignIds.Contains(conversion.AdCampaignId)
                && conversion.OccurredAtUtc >= fromUtc
                && conversion.OccurredAtUtc <= toUtc)
            .GroupBy(conversion => conversion.AdCampaignId)
            .Select(group => new
            {
                CampaignId = group.Key,
                OrderCount = group.Select(conversion => conversion.OrderId).Distinct().Count(),
                Revenue = group.Sum(conversion => conversion.RevenueAmount)
            })
            .ToDictionaryAsync(group => group.CampaignId, group => new { group.OrderCount, group.Revenue }, cancellationToken);

        return campaigns
            .Select(campaign =>
            {
                var campaignImpressions = impressions.GetValueOrDefault(campaign.Id);
                var campaignClicks = clicks.GetValueOrDefault(campaign.Id);
                var campaignSpend = spend.GetValueOrDefault(campaign.Id);
                conversions.TryGetValue(campaign.Id, out var campaignConversions);
                var revenue = campaignConversions?.Revenue ?? 0m;

                return new SellerAdPerformanceDetailResponse(
                    campaign.Id,
                    campaign.Name,
                    campaign.Status.ToString(),
                    campaignImpressions,
                    campaignClicks,
                    campaignImpressions == 0 ? 0 : decimal.Round((decimal)campaignClicks / campaignImpressions, 4),
                    campaignSpend,
                    campaignConversions?.OrderCount ?? 0,
                    revenue,
                    campaignSpend == 0 ? 0 : decimal.Round(revenue / campaignSpend, 4));
            })
            .OrderByDescending(campaign => campaign.RevenueGenerated)
            .ThenByDescending(campaign => campaign.Clicks)
            .ThenBy(campaign => campaign.Name)
            .ToArray();
    }

    private static async Task<SellerCustomerCareSummaryResponse> BuildCustomerCareSummaryAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<Refund> refunds,
        IReadOnlyCollection<ReturnRequest> returns,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var supportTickets = await dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.SellerId == sellerId
                && ticket.OpenedAtUtc >= fromUtc
                && ticket.OpenedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var disputes = await dbContext.Disputes
            .AsNoTracking()
            .Where(dispute => dispute.SellerId == sellerId
                && dispute.OpenedAtUtc >= fromUtc
                && dispute.OpenedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        return new SellerCustomerCareSummaryResponse(
            returns.Count,
            returns.Count(returnRequest => IsOpenReturnStatus(returnRequest.Status)),
            refunds.Count,
            refunds.Sum(refund => refund.Amount),
            supportTickets.Count,
            supportTickets.Count(ticket => !ticket.IsClosed),
            disputes.Count,
            disputes.Count(dispute => dispute.IsActive));
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

    private static IReadOnlyCollection<AnalyticsBucket> BuildBuckets(DateTimeOffset fromUtc, DateTimeOffset toUtc, AnalyticsBucketKind bucket)
    {
        var buckets = new List<AnalyticsBucket>();
        var cursor = fromUtc;
        var step = bucket == AnalyticsBucketKind.Week ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);

        while (cursor <= toUtc)
        {
            var next = cursor.Add(step);
            buckets.Add(new AnalyticsBucket(cursor, next > toUtc ? toUtc : next));
            cursor = next;
        }

        return buckets;
    }

    private static bool IsInBucket(DateTimeOffset value, DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset rangeEndUtc) =>
        value >= startUtc && (endUtc == rangeEndUtc ? value <= endUtc : value < endUtc);

    private static ReportDateRange ResolveRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, TimeProvider timeProvider)
    {
        var resolvedTo = (toUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var resolvedFrom = (fromUtc ?? resolvedTo.AddDays(-30)).ToUniversalTime();
        var isValid = resolvedFrom <= resolvedTo && (resolvedTo - resolvedFrom) <= TimeSpan.FromDays(MaxRangeDays);

        return new ReportDateRange(resolvedFrom, resolvedTo, isValid);
    }

    private static bool TryResolveBucket(string? bucket, out AnalyticsBucketKind bucketKind)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            bucketKind = AnalyticsBucketKind.Day;
            return true;
        }

        return Enum.TryParse(bucket, ignoreCase: true, out bucketKind)
            && Enum.IsDefined(bucketKind);
    }

    private static bool TryResolveReport(string? report, out SellerAnalyticsCsvReport reportKind)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            reportKind = SellerAnalyticsCsvReport.Sales;
            return true;
        }

        return Enum.TryParse(report, ignoreCase: true, out reportKind)
            && Enum.IsDefined(reportKind);
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

    private static bool IsOpenReturnStatus(ReturnStatus status) =>
        status is ReturnStatus.Requested
            or ReturnStatus.AwaitingSellerResponse
            or ReturnStatus.Approved
            or ReturnStatus.ReturnInTransit
            or ReturnStatus.ReturnedToSeller
            or ReturnStatus.RefundPending
            or ReturnStatus.Disputed;

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

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerAnalytics.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult InvalidRangeProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidDateRange",
            detail: $"fromUtc must be earlier than or equal to toUtc, and the range cannot exceed {MaxRangeDays} days.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidBucketProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidBucket",
            detail: "bucket must be Day or Week.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidReportProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidReport",
            detail: "report must be Sales, Products, Inventory, Ads, or Returns.",
            statusCode: StatusCodes.Status400BadRequest);

    private static string BuildCsv(SellerAnalyticsPerformanceResponse analytics, SellerAnalyticsCsvReport report) =>
        report switch
        {
            SellerAnalyticsCsvReport.Sales => BuildSalesCsv(analytics.SalesTrend),
            SellerAnalyticsCsvReport.Products => BuildProductsCsv(analytics.ProductPerformance),
            SellerAnalyticsCsvReport.Inventory => BuildInventoryCsv(analytics.InventoryPerformance),
            SellerAnalyticsCsvReport.Ads => BuildAdsCsv(analytics.AdPerformance),
            SellerAnalyticsCsvReport.Returns => BuildReturnsCsv(analytics.CustomerCareSummary),
            _ => string.Empty
        };

    private static string BuildSalesCsv(IReadOnlyCollection<SellerSalesTrendBucketResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "periodStartUtc", "periodEndUtc", "orderCount", "grossSales", "refundedAmount", "netSales", "unitsSold", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.PeriodStartUtc.ToString("O", CultureInfo.InvariantCulture),
                row.PeriodEndUtc.ToString("O", CultureInfo.InvariantCulture),
                row.OrderCount.ToString(CultureInfo.InvariantCulture),
                row.GrossSales.ToString(CultureInfo.InvariantCulture),
                row.RefundedAmount.ToString(CultureInfo.InvariantCulture),
                row.NetSales.ToString(CultureInfo.InvariantCulture),
                row.UnitsSold.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildProductsCsv(IReadOnlyCollection<SellerProductPerformanceResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "productId", "productTitle", "productSlug", "status", "unitsSold", "grossSales", "refundedAmount", "returnCount", "returnRate", "stockQuantity", "reservedQuantity", "availableQuantity", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.ProductId.ToString(),
                row.ProductTitle ?? string.Empty,
                row.ProductSlug ?? string.Empty,
                row.Status,
                row.UnitsSold.ToString(CultureInfo.InvariantCulture),
                row.GrossSales.ToString(CultureInfo.InvariantCulture),
                row.RefundedAmount.ToString(CultureInfo.InvariantCulture),
                row.ReturnCount.ToString(CultureInfo.InvariantCulture),
                row.ReturnRate.ToString(CultureInfo.InvariantCulture),
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                row.ReservedQuantity.ToString(CultureInfo.InvariantCulture),
                row.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildInventoryCsv(IReadOnlyCollection<SellerInventoryPerformanceResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "productId", "productTitle", "productVariantId", "sku", "barcode", "size", "colour", "status", "stockQuantity", "reservedQuantity", "availableQuantity", "isLowStock", "isOutOfStock", "lastMovementAtUtc");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.ProductId.ToString(),
                row.ProductTitle ?? string.Empty,
                row.ProductVariantId.ToString(),
                row.Sku,
                row.Barcode ?? string.Empty,
                row.Size,
                row.Colour,
                row.Status,
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                row.ReservedQuantity.ToString(CultureInfo.InvariantCulture),
                row.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                row.IsLowStock.ToString(CultureInfo.InvariantCulture),
                row.IsOutOfStock.ToString(CultureInfo.InvariantCulture),
                row.LastMovementAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return builder.ToString();
    }

    private static string BuildAdsCsv(IReadOnlyCollection<SellerAdPerformanceDetailResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "adCampaignId", "name", "status", "impressions", "clicks", "clickThroughRate", "spend", "ordersGenerated", "revenueGenerated", "returnOnAdSpend", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.AdCampaignId.ToString(),
                row.Name,
                row.Status,
                row.Impressions.ToString(CultureInfo.InvariantCulture),
                row.Clicks.ToString(CultureInfo.InvariantCulture),
                row.ClickThroughRate.ToString(CultureInfo.InvariantCulture),
                row.Spend.ToString(CultureInfo.InvariantCulture),
                row.OrdersGenerated.ToString(CultureInfo.InvariantCulture),
                row.RevenueGenerated.ToString(CultureInfo.InvariantCulture),
                row.ReturnOnAdSpend.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildReturnsCsv(SellerCustomerCareSummaryResponse summary)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "returnCount", "openReturnCount", "refundCount", "refundedAmount", "supportTicketCount", "openSupportTicketCount", "disputeCount", "activeDisputeCount", "currency");
        AppendCsvLine(
            builder,
            summary.ReturnCount.ToString(CultureInfo.InvariantCulture),
            summary.OpenReturnCount.ToString(CultureInfo.InvariantCulture),
            summary.RefundCount.ToString(CultureInfo.InvariantCulture),
            summary.RefundedAmount.ToString(CultureInfo.InvariantCulture),
            summary.SupportTicketCount.ToString(CultureInfo.InvariantCulture),
            summary.OpenSupportTicketCount.ToString(CultureInfo.InvariantCulture),
            summary.DisputeCount.ToString(CultureInfo.InvariantCulture),
            summary.ActiveDisputeCount.ToString(CultureInfo.InvariantCulture),
            DefaultCurrency);
        return builder.ToString();
    }

    private static void AppendCsvLine(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(Csv)));
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private enum AnalyticsBucketKind
    {
        Day,
        Week
    }

    private enum SellerAnalyticsCsvReport
    {
        Sales,
        Products,
        Inventory,
        Ads,
        Returns
    }

    private sealed record ReportDateRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc, bool IsValid);

    private sealed record AnalyticsBucket(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);

    private sealed record ProductAnalyticsProjection(
        Guid ProductId,
        string? Title,
        string? Slug,
        ProductStatus Status);

    private sealed record VariantAnalyticsProjection(
        Guid ProductVariantId,
        Guid ProductId,
        string Sku,
        string? Barcode,
        string Size,
        string Colour,
        ProductVariantStatus Status,
        int StockQuantity,
        int ReservedQuantity,
        DateTimeOffset UpdatedAtUtc);

    private sealed record InventoryMovementLastActivityProjection(
        Guid ProductVariantId,
        DateTimeOffset LastMovementAtUtc);
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

public sealed record SellerAnalyticsPerformanceResponse(
    Guid SellerId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Bucket,
    IReadOnlyCollection<SellerSalesTrendBucketResponse> SalesTrend,
    IReadOnlyCollection<SellerProductPerformanceResponse> ProductPerformance,
    IReadOnlyCollection<SellerInventoryPerformanceResponse> InventoryPerformance,
    IReadOnlyCollection<SellerAdPerformanceDetailResponse> AdPerformance,
    SellerCustomerCareSummaryResponse CustomerCareSummary);

public sealed record SellerSalesTrendBucketResponse(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int OrderCount,
    decimal GrossSales,
    decimal RefundedAmount,
    decimal NetSales,
    int UnitsSold);

public sealed record SellerProductPerformanceResponse(
    Guid ProductId,
    string? ProductTitle,
    string? ProductSlug,
    string Status,
    int UnitsSold,
    decimal GrossSales,
    decimal RefundedAmount,
    int ReturnCount,
    decimal ReturnRate,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity);

public sealed record SellerInventoryPerformanceResponse(
    Guid ProductId,
    string? ProductTitle,
    Guid ProductVariantId,
    string Sku,
    string? Barcode,
    string Size,
    string Colour,
    string Status,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    bool IsLowStock,
    bool IsOutOfStock,
    DateTimeOffset? LastMovementAtUtc);

public sealed record SellerAdPerformanceDetailResponse(
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

public sealed record SellerCustomerCareSummaryResponse(
    int ReturnCount,
    int OpenReturnCount,
    int RefundCount,
    decimal RefundedAmount,
    int SupportTicketCount,
    int OpenSupportTicketCount,
    int DisputeCount,
    int ActiveDisputeCount);
