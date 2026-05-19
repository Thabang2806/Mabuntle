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
using Swyftly.Api.Payouts;
using Swyftly.Application.Identity;
using Swyftly.Application.Ledger;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class PayoutTests
{
    [Fact]
    public async Task Seller_CanViewBalanceAndPayouts()
    {
        await using var factory = new PayoutTestFactory();
        using var client = factory.CreateClient();
        var sellerUserId = await CreateSellerUserAsync(factory, client, "seller-payout@example.test");
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        await SeedPayoutAsync(factory, sellerId, 875m);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, "seller-payout@example.test"));

        using var balanceResponse = await client.GetAsync("/api/seller/balance");
        using var payoutsResponse = await client.GetAsync("/api/seller/payouts");

        balanceResponse.EnsureSuccessStatusCode();
        payoutsResponse.EnsureSuccessStatusCode();
        var balance = await balanceResponse.Content.ReadFromJsonAsync<SellerBalanceResponse>();
        var payouts = await payoutsResponse.Content.ReadFromJsonAsync<SellerPayoutResponse[]>();
        Assert.NotNull(balance);
        Assert.Equal(875m, Assert.Single(balance!.Balances).PendingBalance);
        Assert.NotNull(payouts);
        Assert.Equal("Pending", Assert.Single(payouts!).Status);
    }

    [Fact]
    public async Task Admin_CanHoldAndReleasePayout_AndAuditLogsAreWritten()
    {
        await using var factory = new PayoutTestFactory();
        using var client = factory.CreateClient();
        var sellerUserId = await CreateSellerUserAsync(factory, client, "seller-admin-payout@example.test");
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var payoutId = await SeedPayoutAsync(factory, sellerId, 875m);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginAdminAsync(factory, client));

        using var holdResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/hold",
            new PayoutReasonRequest("Dispute review."));
        holdResponse.EnsureSuccessStatusCode();
        var held = await holdResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(held);
        Assert.Equal("OnHold", held!.Status);

        using var releaseResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/release",
            new PayoutReasonRequest("Review complete."));
        releaseResponse.EnsureSuccessStatusCode();
        var released = await releaseResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(released);
        Assert.Equal("Pending", released!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
        Assert.Equal(875m, balance.PendingBalance);
        Assert.Equal(0m, balance.HeldBalance);
        Assert.Equal(2, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.EntityType == "SellerPayout"));
    }

    private static async Task<Guid> CreateSellerUserAsync(PayoutTestFactory factory, HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Seller));
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId != Guid.Empty)
            .OrderByDescending(seller => seller.CreatedAtUtc)
            .Select(seller => seller.UserId)
            .FirstAsync();
    }

    private static async Task<Guid> GetSellerIdAsync(PayoutTestFactory factory, Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == sellerUserId)
            .Select(seller => seller.Id)
            .SingleAsync();
    }

    private static async Task<Guid> SeedPayoutAsync(PayoutTestFactory factory, Guid sellerId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(amount);
        var ledgerEntry = new LedgerEntry(
            null,
            null,
            sellerId,
            null,
            null,
            LedgerEntryType.SellerPendingBalanceCredited,
            amount,
            "ZAR",
            LedgerDirection.Credit,
            "Seller pending balance credited.",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        var payout = new SellerPayout(sellerId, amount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payout.AddItem(ledgerEntry.Id, null, null, amount, DateTimeOffset.Parse("2026-05-18T12:00:00Z"));

        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return payout.Id;
    }

    private static async Task<string> CreateAndLoginAdminAsync(PayoutTestFactory factory, HttpClient client)
    {
        var email = $"admin-payout-{Guid.NewGuid():N}@example.test";
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
            var createResult = await userManager.CreateAsync(admin, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(admin, SwyftlyRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class PayoutTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyPayoutTests_{Guid.NewGuid():N}";

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
