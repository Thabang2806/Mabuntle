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
using Swyftly.Api.Refunds;
using Swyftly.Application.Identity;
using Swyftly.Application.Refunds;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class RefundTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task AdminCanApproveFullRefund_AndLedgerReversalsAdjustSellerBalance()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceOperator));

        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(1000m, "Approved full refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);
        Assert.Equal("Requested", requested!.Status);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceApprover));
        using var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested.RefundId}/approve",
            new ApproveRefundApiRequest("Return approved by admin."));
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(approved);
        Assert.Equal("Refunded", approved!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var payment = await dbContext.Payments.SingleAsync(payment => payment.Id == seed.PaymentId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == seed.OrderId);
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == seed.SellerId);
        var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == seed.SellerId);
        var refundReversals = await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundReversal)
            .ToListAsync();
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(0m, balance.PendingBalance);
        Assert.Equal(0m, payout.Amount);
        Assert.Equal(SellerPayoutStatus.Reversed, payout.Status);
        Assert.Equal(1, await dbContext.SellerPayoutAdjustments.CountAsync(adjustment => adjustment.RefundId == approved.RefundId));
        Assert.Contains(refundReversals, entry => entry.Amount == 875m && entry.Direction == LedgerDirection.Debit);
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "RefundApproved"));
    }

    [Fact]
    public async Task AdminCanApprovePartialRefund_AndSellerBalanceUsesProportionalDebit()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceOperator));

        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(500m, "Approved partial refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceApprover));
        using var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested!.RefundId}/approve",
            new ApproveRefundApiRequest("Partial refund approved by admin."));
        approveResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var payment = await dbContext.Payments.SingleAsync(payment => payment.Id == seed.PaymentId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == seed.OrderId);
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == seed.SellerId);
        var payout = await dbContext.SellerPayouts.Include(payout => payout.Items).SingleAsync(payout => payout.SellerId == seed.SellerId);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(437.50m, balance.PendingBalance);
        Assert.Equal(437.50m, payout.Amount);
        Assert.Equal(437.50m, payout.Items.Single().AdjustedAmount);
        Assert.Equal(437.50m, await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == seed.PaymentId
                && entry.Type == LedgerEntryType.RefundReversal
                && entry.Description == "Seller balance refund reversal.")
            .Select(entry => entry.Amount)
            .SingleAsync());
    }

    [Fact]
    public async Task AdminDuplicateRefundApproval_ReturnsExistingRefundWithoutDuplicateLedgerOrAudit()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceOperator));
        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(500m, "Approved duplicate-check refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, SwyftlyRoles.FinanceApprover));
        using var firstApproveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested!.RefundId}/approve",
            new ApproveRefundApiRequest("First approval."));
        using var secondApproveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested.RefundId}/approve",
            new ApproveRefundApiRequest("Duplicate approval."));

        firstApproveResponse.EnsureSuccessStatusCode();
        secondApproveResponse.EnsureSuccessStatusCode();
        var first = await firstApproveResponse.Content.ReadFromJsonAsync<RefundResult>();
        var second = await secondApproveResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.ProviderRefundReference, second!.ProviderRefundReference);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Equal(1, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundIssued));
        Assert.Equal(3, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundReversal));
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "RefundApproved" && auditLog.EntityId == requested.RefundId.ToString()));
    }

    private static async Task<SeededRefundOrder> SeedPaidOrderAsync(RefundTestFactory factory, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Refundable Item", "SKU-REFUND", "M", "Black", amount, 1);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(2), "TestShipped");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(3), "TestDelivered");
        var payment = new Payment(order.Id, buyerId, "Fake", amount, "ZAR", now);
        payment.SetProviderReference($"fake_{order.Id:N}", now);
        payment.MarkPaid(now);
        var sellerPending = 875m;
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(sellerPending);
        var buyerPaymentEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.BuyerPaymentReceived, 1000m, "ZAR", LedgerDirection.Credit, "Buyer payment received.", now);
        var platformCommissionEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PlatformCommissionRecorded, 100m, "ZAR", LedgerDirection.Credit, "Platform commission recorded.", now);
        var providerFeeEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PaymentProviderFeeRecorded, 25m, "ZAR", LedgerDirection.Debit, "Payment provider fee recorded.", now);
        var sellerPendingEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.SellerPendingBalanceCredited, sellerPending, "ZAR", LedgerDirection.Credit, "Seller pending balance credited.", now);
        var payout = new SellerPayout(sellerId, sellerPending, "ZAR", now);
        payout.AddItem(sellerPendingEntry.Id, order.Id, payment.Id, sellerPending, now);

        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.AddRange(
            buyerPaymentEntry,
            platformCommissionEntry,
            providerFeeEntry,
            sellerPendingEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return new SeededRefundOrder(order.Id, payment.Id, sellerId);
    }

    private static async Task<string> CreateAndLoginFinanceUserAsync(RefundTestFactory factory, HttpClient client, string role)
    {
        var email = $"finance-refund-{Guid.NewGuid():N}@example.test";
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
            var roleResult = await userManager.AddToRoleAsync(admin, role);
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

    private sealed record SeededRefundOrder(Guid OrderId, Guid PaymentId, Guid SellerId);

    private sealed class RefundTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyRefundTests_{Guid.NewGuid():N}";

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
