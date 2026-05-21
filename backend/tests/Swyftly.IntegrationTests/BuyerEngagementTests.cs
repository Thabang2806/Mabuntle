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
using Swyftly.Api.Admin;
using Swyftly.Api.Buyers;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;
using DomainNotification = Swyftly.Domain.Notifications.Notification;

namespace Swyftly.IntegrationTests;

public sealed class BuyerEngagementTests
{
    [Fact]
    public async Task Buyer_CanAddListAndRemoveWishlistItem()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-buyer@example.test", SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Wishlist Seller", "wishlist-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Wishlist Dress", "wishlist-dress", ProductSeedStatus.Published);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var addResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        addResponse.EnsureSuccessStatusCode();
        var added = await addResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse>();
        Assert.NotNull(added);
        Assert.Equal(productId, added!.Product.ProductId);

        using var duplicateResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        duplicateResponse.EnsureSuccessStatusCode();
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse>();
        Assert.Equal(added.WishlistItemId, duplicate!.WishlistItemId);

        using var listResponse = await client.GetAsync("/api/buyer/wishlist");
        listResponse.EnsureSuccessStatusCode();
        var wishlist = await listResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        var item = Assert.Single(wishlist!);
        Assert.Equal(productId, item.Product.ProductId);

        using var removeResponse = await client.DeleteAsync($"/api/buyer/wishlist/{productId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        using var emptyResponse = await client.GetAsync("/api/buyer/wishlist");
        emptyResponse.EnsureSuccessStatusCode();
        var emptyWishlist = await emptyResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        Assert.Empty(emptyWishlist!);
    }

    [Fact]
    public async Task Wishlist_RejectsProductsThatAreNotPubliclyVisible()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-hidden-buyer@example.test", SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(
            factory,
            "Hidden Wishlist Seller",
            "hidden-wishlist-seller",
            publishStorefront: false);
        var productId = await CreateProductAsync(factory, sellerId, "Hidden Dress", "hidden-dress", ProductSeedStatus.Published);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_CanCreateUpdateAndDeleteVerifiedPurchaseReview()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Review Seller", "review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Review Dress", "review-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(5, "Great fit", "The dress matched the description."));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.NotNull(created);
        Assert.Equal(productId, created!.ProductId);
        Assert.Equal("PendingReview", created.Status);

        using var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(4, "Duplicate", "Second review."));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/buyer/reviews/{created.ReviewId}",
            new ProductReviewRequest(4, "Updated fit", "Still happy after a second look."));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.Equal(4, updated!.Rating);
        Assert.Equal("Updated fit", updated.Title);
        Assert.Equal("PendingReview", updated.Status);

        using var deleteResponse = await client.DeleteAsync($"/api/buyer/reviews/{created.ReviewId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var buyerReviewsResponse = await client.GetAsync("/api/buyer/reviews");
        buyerReviewsResponse.EnsureSuccessStatusCode();
        var buyerReviews = await buyerReviewsResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse[]>();
        Assert.Empty(buyerReviews!);
    }

    [Fact]
    public async Task Buyer_CannotReviewUndeliveredOrOtherBuyerOrders()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string ownerEmail = "review-owner@example.test";
        const string otherEmail = "review-other@example.test";
        await RegisterAndLoginAsync(client, ownerEmail, SwyftlyRoles.Buyer);
        var otherToken = await RegisterAndLoginAsync(client, otherEmail, SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Review Guard Seller", "review-guard-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Guard Dress", "guard-dress", ProductSeedStatus.Published);
        var owner = await GetBuyerAsync(factory, ownerEmail);
        var pendingOrder = await CreatePendingOrderAsync(factory, owner.Id, sellerId, productId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        using var otherBuyerResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{pendingOrder.OrderId}/items/{pendingOrder.OrderItemId}/review",
            new ProductReviewRequest(5, "Not mine", "This should not be allowed."));
        Assert.Equal(HttpStatusCode.NotFound, otherBuyerResponse.StatusCode);

        var ownerToken = await LoginAsync(client, ownerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var undeliveredResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{pendingOrder.OrderId}/items/{pendingOrder.OrderItemId}/review",
            new ProductReviewRequest(5, "Too soon", "This should not be allowed yet."));
        Assert.Equal(HttpStatusCode.Conflict, undeliveredResponse.StatusCode);
    }

    [Fact]
    public async Task PublicReviewReads_ReturnOnlyPublishedReviewsAndSummary()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory, "Public Review Seller", "public-review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Public Review Dress", "public-review-dress", ProductSeedStatus.Published);
        await SeedReviewsAsync(factory, productId, sellerId);

        using var listResponse = await client.GetAsync("/api/products/public-review-dress/reviews");
        listResponse.EnsureSuccessStatusCode();
        var reviews = await listResponse.Content.ReadFromJsonAsync<PublicProductReviewResponse[]>();
        var review = Assert.Single(reviews!);
        Assert.Equal(5, review.Rating);

        using var summaryResponse = await client.GetAsync("/api/products/public-review-dress/review-summary");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<PublicProductReviewSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.ReviewCount);
        Assert.Equal(5, summary.AverageRating);
        Assert.Equal(1, summary.RatingCounts.Single(count => count.Rating == 5).Count);
    }

    [Fact]
    public async Task AdminCanModeratePendingBuyerReviews_AndBuyerIsNotified()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var buyerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        const string buyerEmail = "moderated-review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(buyerClient, buyerEmail, SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Moderation Seller", "moderation-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Moderated Dress", "moderated-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(5, "Helpful fit", "The product matched the photos."));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.Equal("PendingReview", created!.Status);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "review-admin@example.test", SwyftlyRoles.Admin));

        using var pendingResponse = await adminClient.GetAsync("/api/admin/reviews/pending");
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<AdminProductReviewDetailResponse[]>();
        Assert.Contains(pending!, review => review.ReviewId == created.ReviewId);

        using var approveResponse = await adminClient.PostAsync($"/api/admin/reviews/{created.ReviewId}/approve", null);
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<AdminProductReviewDetailResponse>();
        Assert.Equal("Published", approved!.Status);
        Assert.Contains(approved.AuditTrail, audit => audit.ActionType == "ProductReviewApproved");

        using var publicResponse = await buyerClient.GetAsync("/api/products/moderated-dress/reviews");
        publicResponse.EnsureSuccessStatusCode();
        var publicReviews = await publicResponse.Content.ReadFromJsonAsync<PublicProductReviewResponse[]>();
        Assert.Contains(publicReviews!, review => review.ReviewId == created.ReviewId);

        using var notificationResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationResponse.EnsureSuccessStatusCode();
        var notifications = await notificationResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReviewApproved" && notification.RelatedEntityId == created.ReviewId);
    }

    [Fact]
    public async Task AdminCanRejectPendingBuyerReview_AndBuyerSeesReason()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var buyerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        const string buyerEmail = "rejected-review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(buyerClient, buyerEmail, SwyftlyRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Rejected Review Seller", "rejected-review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Rejected Review Dress", "rejected-review-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(2, "Bad words", "Needs moderation."));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "reject-review-admin@example.test", SwyftlyRoles.Admin));
        using var rejectResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/reviews/{created!.ReviewId}/reject",
            new AdminProductReviewReasonRequest("Please remove personal information."));
        rejectResponse.EnsureSuccessStatusCode();

        using var buyerReviewsResponse = await buyerClient.GetAsync("/api/buyer/reviews");
        buyerReviewsResponse.EnsureSuccessStatusCode();
        var buyerReviews = await buyerReviewsResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse[]>();
        var rejected = Assert.Single(buyerReviews!);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Please remove personal information.", rejected.ModerationReason);

        using var notificationResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationResponse.EnsureSuccessStatusCode();
        var notifications = await notificationResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReviewRejected" && notification.RelatedEntityId == created.ReviewId);
    }

    [Fact]
    public async Task Buyer_CanListAndMarkNotificationsRead()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "notification-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, SwyftlyRoles.Buyer);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var notificationIds = await SeedNotificationsAsync(factory, buyer.UserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var listResponse = await client.GetAsync("/api/buyer/notifications");
        listResponse.EnsureSuccessStatusCode();
        var notifications = await listResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Equal(2, notifications!.Length);
        Assert.All(notifications, notification => Assert.Null(notification.ReadAtUtc));

        using var readResponse = await client.PostAsync($"/api/buyer/notifications/{notificationIds[0]}/read", null);
        readResponse.EnsureSuccessStatusCode();
        var read = await readResponse.Content.ReadFromJsonAsync<NotificationResult>();
        Assert.NotNull(read!.ReadAtUtc);

        using var readAllResponse = await client.PostAsync("/api/buyer/notifications/read-all", null);
        readAllResponse.EnsureSuccessStatusCode();
        var readAll = await readAllResponse.Content.ReadFromJsonAsync<NotificationsReadAllResponse>();
        Assert.Equal(1, readAll!.UpdatedCount);
    }

    [Fact]
    public async Task BuyerEndpoints_RejectAnonymousAndNonBuyerUsers()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();

        using var anonymousResponse = await client.GetAsync("/api/buyer/wishlist");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var sellerToken = await RegisterAndLoginAsync(client, "engagement-seller@example.test", SwyftlyRoles.Seller);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        using var sellerResponse = await client.GetAsync("/api/buyer/wishlist");
        Assert.Equal(HttpStatusCode.Forbidden, sellerResponse.StatusCode);
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task<string> CreateAndLoginUserInRoleAsync(
        BuyerEngagementTestFactory factory,
        HttpClient client,
        string email,
        string role)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createResult = await userManager.CreateAsync(user, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<BuyerProfile> GetBuyerAsync(BuyerEngagementTestFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.Email == email);
        return await dbContext.BuyerProfiles.SingleAsync(buyer => buyer.UserId == user.Id);
    }

    private static async Task<Guid> CreateSellerAsync(
        BuyerEngagementTestFactory factory,
        string storeName,
        string storeSlug,
        bool publishStorefront = true)
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

        if (publishStorefront)
        {
            storefront.Publish();
        }

        dbContext.AddRange(seller, storefront, address, payout);
        await dbContext.SaveChangesAsync();
        return seller.Id;
    }

    private static async Task<Guid> CreateProductAsync(
        BuyerEngagementTestFactory factory,
        Guid sellerId,
        string title,
        string slug,
        ProductSeedStatus status)
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
            "A dress for buyer engagement testing.");
        product.UpdateTags("[\"dress\",\"review\"]");

        if (status == ProductSeedStatus.Published)
        {
            product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
            product.Publish(DateTimeOffset.UtcNow);
        }

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499m,
            599m,
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

    private static async Task<OrderSeed> CreateDeliveredOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId) =>
        await CreateOrderAsync(factory, buyerId, sellerId, productId, delivered: true);

    private static async Task<OrderSeed> CreatePendingOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId) =>
        await CreateOrderAsync(factory, buyerId, sellerId, productId, delivered: false);

    private static async Task<OrderSeed> CreateOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId,
        bool delivered)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var now = DateTimeOffset.UtcNow;
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(productId, Guid.NewGuid(), "Review Dress", "SKU-ORDER-1", "M", "Black", 499m, 1);
        if (delivered)
        {
            order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "Paid");
            order.ChangeStatus(OrderStatus.Processing, now.AddMinutes(2), "Processing");
            order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(3), "Shipped");
            order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(4), "Delivered");
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return new OrderSeed(order.Id, order.Items.Single().Id);
    }

    private static async Task SeedReviewsAsync(BuyerEngagementTestFactory factory, Guid productId, Guid sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var publicReview = new ProductReview(
            buyer.Id,
            sellerId,
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            5,
            "Excellent",
            "Loved the fit.",
            now);
        publicReview.Approve(Guid.NewGuid(), now.AddMinutes(1));
        var removedReview = new ProductReview(
            buyer.Id,
            sellerId,
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Removed",
            "This should not be public.",
            now);
        removedReview.Remove(now.AddMinutes(1));

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.ProductReviews.AddRange(publicReview, removedReview);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<Guid>> SeedNotificationsAsync(BuyerEngagementTestFactory factory, Guid recipientUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var first = new DomainNotification(
            recipientUserId,
            "OrderUpdate",
            "Order shipped",
            "Your order has shipped.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var second = new DomainNotification(
            recipientUserId,
            "Support",
            "Support reply",
            "Support replied to your ticket.",
            "SupportTicket",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(1));

        dbContext.Notifications.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        return [first.Id, second.Id];
    }

    private enum ProductSeedStatus
    {
        Draft,
        Published
    }

    private sealed record OrderSeed(Guid OrderId, Guid OrderItemId);

    private sealed class BuyerEngagementTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyBuyerEngagementTests_{Guid.NewGuid():N}";

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
