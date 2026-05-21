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
using Swyftly.Api.Returns;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Application.Returns;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class ReturnTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanRequestReturnForDeliveredOrder_AndSellerPayoutIsHeld()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-return@example.test", SwyftlyRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-return@example.test", SwyftlyRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);
        await SeedPayoutAsync(factory, sellerId, order.OrderId, order.OrderItemId, 875m);

        using var response = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "DamagedItem",
                "The item arrived damaged.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "DamagedItem", false, "Torn seam.")
                ]));

        response.EnsureSuccessStatusCode();
        var returnRequest = await response.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);
        Assert.Equal("AwaitingSellerResponse", returnRequest!.Status);
        Assert.Single(returnRequest.Items);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
            var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
            var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == sellerId);
            Assert.Equal(0m, balance.PendingBalance);
            Assert.Equal(875m, balance.HeldBalance);
            Assert.Equal(SellerPayoutStatus.OnHold, payout.Status);
            Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "PayoutHeld"));
        }

        using var sellerResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest.ReturnRequestId}/approve",
            new SellerReturnResponseApiRequest("Return approved."));

        sellerResponse.EnsureSuccessStatusCode();
        var approved = await sellerResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(approved);
        Assert.Equal("Approved", approved!.Status);
        Assert.Contains(approved.Messages, message => message.SenderRole == "Seller");

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReturnApproved" && notification.RelatedEntityId == returnRequest.ReturnRequestId);
    }

    [Fact]
    public async Task BuyerCannotRequestReturnForUndeliveredOrder()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        var sellerUserId = await RegisterSellerAsync(factory, "seller-undelivered-return@example.test");
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-undelivered-return@example.test", SwyftlyRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedOrderAsync(factory, buyerId, sellerId, OrderStatus.Shipped);

        using var response = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "WrongSize",
                "Size issue.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "WrongSize", false, null)
                ]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanViewDisputedReturns()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-disputed-return@example.test", SwyftlyRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-disputed-return@example.test", SwyftlyRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "NotAsDescribed",
                "Listing did not match.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "NotAsDescribed", false, null)
                ]));
        createResponse.EnsureSuccessStatusCode();
        var returnRequest = await createResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);

        using var rejectResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest!.ReturnRequestId}/reject",
            new SellerReturnResponseApiRequest("Seller rejects the claim."));
        rejectResponse.EnsureSuccessStatusCode();

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReturnRejected" && notification.RelatedEntityId == returnRequest.ReturnRequestId);

        using var disputeResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/returns/{returnRequest.ReturnRequestId}/dispute",
            new DisputeReturnRequestApiRequest("Please review the listing photos."));
        disputeResponse.EnsureSuccessStatusCode();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginAdminAsync(factory, adminClient));
        using var adminResponse = await adminClient.GetAsync("/api/admin/returns/disputed");

        adminResponse.EnsureSuccessStatusCode();
        var disputedReturns = await adminResponse.Content.ReadFromJsonAsync<ReturnRequestResult[]>();
        Assert.NotNull(disputedReturns);
        var disputed = Assert.Single(disputedReturns!);
        Assert.Equal("Disputed", disputed.Status);
        Assert.Equal(returnRequest.ReturnRequestId, disputed.ReturnRequestId);
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private static async Task<Guid> RegisterSellerAsync(ReturnTestFactory factory, string email)
    {
        using var client = factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, email, SwyftlyRoles.Seller);
        return auth.UserId;
    }

    private static async Task<Guid> GetSellerIdAsync(ReturnTestFactory factory, Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == sellerUserId)
            .Select(seller => seller.Id)
            .SingleAsync();
    }

    private static async Task<Guid> GetBuyerIdAsync(ReturnTestFactory factory, Guid buyerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        return await dbContext.BuyerProfiles
            .Where(buyer => buyer.UserId == buyerUserId)
            .Select(buyer => buyer.Id)
            .SingleAsync();
    }

    private static Task<SeededOrder> SeedDeliveredOrderAsync(ReturnTestFactory factory, Guid buyerId, Guid sellerId) =>
        SeedOrderAsync(factory, buyerId, sellerId, OrderStatus.Delivered);

    private static async Task<SeededOrder> SeedOrderAsync(
        ReturnTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        OrderStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Returned Item", "SKU-RETURN", "M", "Black", 1000m, 1);
        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        }

        if (status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(2), "TestShipped");
        }

        if (status == OrderStatus.Delivered)
        {
            order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(3), "TestDelivered");
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return new SeededOrder(order.Id, order.Items.Single().Id);
    }

    private static async Task SeedPayoutAsync(
        ReturnTestFactory factory,
        Guid sellerId,
        Guid orderId,
        Guid orderItemId,
        decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:05:00Z");
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(amount);
        var ledgerEntry = new LedgerEntry(
            orderId,
            orderItemId,
            sellerId,
            null,
            null,
            LedgerEntryType.SellerPendingBalanceCredited,
            amount,
            "ZAR",
            LedgerDirection.Credit,
            "Seller pending balance credited.",
            createdAt);
        var payout = new SellerPayout(sellerId, amount, "ZAR", createdAt);
        payout.AddItem(ledgerEntry.Id, orderId, null, amount, createdAt);

        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> CreateAndLoginAdminAsync(ReturnTestFactory factory, HttpClient client)
    {
        var email = $"admin-return-{Guid.NewGuid():N}@example.test";
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
            var createResult = await userManager.CreateAsync(admin, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(admin, SwyftlyRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed record SeededOrder(Guid OrderId, Guid OrderItemId);

    private sealed class ReturnTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyReturnTests_{Guid.NewGuid():N}";

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
