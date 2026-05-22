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
using Swyftly.Api.Payouts;
using Swyftly.Api.Sellers;
using Swyftly.Application.Identity;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class SellerPayoutProfileChangeTests
{
    [Fact]
    public async Task VerifiedSeller_CanSubmitPayoutProfileChange_AndFinanceCanApprove()
    {
        await using var factory = new SellerPayoutProfileChangeTestFactory();
        using var client = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(factory, client, "seller-payout-change@example.test", SwyftlyRoles.Seller);
        var sellerId = await VerifySellerAsync(factory, sellerAuth.UserId, "provider-ref-current");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerAuth.AccessToken);
        using var draftResponse = await client.PutAsJsonAsync(
            "/api/seller/payout-profile/change-request",
            new SellerPayoutProfileChangeRequestRequest(
                "provider-ref-next",
                "Updated payout provider reference."));
        draftResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/seller/payout-profile/change-request/submit-review",
            new { });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SellerPayoutProfileChangeStateResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("PendingReview", submitted!.ActiveRequest?.Status);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, SwyftlyRoles.FinanceOperator));
        using var queueResponse = await client.GetAsync("/api/admin/sellers/payout-profile-change-requests");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<AdminPayoutProfileChangeRequestResponse[]>();
        var queued = Assert.Single(queue!);
        Assert.Equal(sellerId, queued.SellerId);
        Assert.Equal("provider-ref-current", queued.CurrentPayoutProviderReference);
        Assert.Equal("provider-ref-next", queued.ProposedPayoutProviderReference);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, SwyftlyRoles.FinanceApprover));
        using var approveResponse = await client.PostAsJsonAsync(
            $"/api/admin/sellers/payout-profile-change-requests/{queued.RequestId}/approve",
            new PayoutProfileChangeReviewRequest("Verified updated provider reference."));
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<AdminPayoutProfileChangeRequestResponse>();
        Assert.NotNull(approved);
        Assert.Equal("Approved", approved!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var payoutProfile = await dbContext.SellerPayoutProfiles.SingleAsync(profile => profile.SellerId == sellerId);
        Assert.Equal("provider-ref-next", payoutProfile.PayoutProviderReference);
        Assert.True(payoutProfile.IsAdminApproved);
        Assert.Equal(3, await dbContext.AuditLogs.CountAsync(log => log.EntityType == "SellerPayoutProfileChangeRequest"));
    }

    [Fact]
    public async Task VerifiedSeller_CannotUseOnboardingPayoutEndpoint()
    {
        await using var factory = new SellerPayoutProfileChangeTestFactory();
        using var client = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(factory, client, "verified-onboarding-payout@example.test", SwyftlyRoles.Seller);
        await VerifySellerAsync(factory, sellerAuth.UserId, "provider-ref-current");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerAuth.AccessToken);
        using var response = await client.PutAsJsonAsync(
            "/api/seller/onboarding/payout",
            new UpdateSellerPayoutRequest("provider-ref-direct"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RequesterCannotApproveOwnPayoutProfileChange()
    {
        await using var factory = new SellerPayoutProfileChangeTestFactory();
        using var client = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(
            factory,
            client,
            "seller-finance-payout-change@example.test",
            SwyftlyRoles.Seller,
            SwyftlyRoles.FinanceApprover);
        await VerifySellerAsync(factory, sellerAuth.UserId, "provider-ref-current");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerAuth.AccessToken);
        using var draftResponse = await client.PutAsJsonAsync(
            "/api/seller/payout-profile/change-request",
            new SellerPayoutProfileChangeRequestRequest("provider-ref-next", "Updated payout provider reference."));
        draftResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/seller/payout-profile/change-request/submit-review",
            new { });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SellerPayoutProfileChangeStateResponse>();
        var requestId = submitted!.ActiveRequest!.RequestId;

        using var approveResponse = await client.PostAsJsonAsync(
            $"/api/admin/sellers/payout-profile-change-requests/{requestId}/approve",
            new PayoutProfileChangeReviewRequest("Trying to self-approve."));

        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
    }

    [Fact]
    public async Task PendingPayoutProfileChange_BlocksPayoutProcessingWithoutHoldingPayout()
    {
        await using var factory = new SellerPayoutProfileChangeTestFactory();
        using var client = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(factory, client, "seller-blocked-payout@example.test", SwyftlyRoles.Seller);
        var sellerId = await VerifySellerAsync(factory, sellerAuth.UserId, "provider-ref-current");
        var payoutId = await SeedPayoutAsync(factory, sellerId, 875m);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerAuth.AccessToken);
        using var draftResponse = await client.PutAsJsonAsync(
            "/api/seller/payout-profile/change-request",
            new SellerPayoutProfileChangeRequestRequest("provider-ref-next", "Updated payout provider reference."));
        draftResponse.EnsureSuccessStatusCode();
        using var submitResponse = await client.PostAsJsonAsync(
            "/api/seller/payout-profile/change-request/submit-review",
            new { });
        submitResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, SwyftlyRoles.FinanceOperator));
        using var makeAvailableResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/make-available",
            new PayoutReasonRequest("Settlement window reached."));
        makeAvailableResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, SwyftlyRoles.FinanceApprover));
        using var processResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/process",
            new PayoutReasonRequest("Approved for payout."));

        Assert.Equal(HttpStatusCode.Conflict, processResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var payout = await dbContext.SellerPayouts.SingleAsync(item => item.Id == payoutId);
        var balance = await dbContext.SellerBalances.SingleAsync(item => item.SellerId == sellerId);
        Assert.Equal(SellerPayoutStatus.Available, payout.Status);
        Assert.Equal(875m, balance.AvailableBalance);
        Assert.Equal(0m, balance.HeldBalance);
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(
        SellerPayoutProfileChangeTestFactory factory,
        HttpClient client,
        string email,
        string role,
        params string[] extraRoles)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registered);

        if (extraRoles.Length > 0)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(registered!.UserId.ToString());
            Assert.NotNull(user);
            foreach (var extraRole in extraRoles)
            {
                var roleResult = await userManager.AddToRoleAsync(user!, extraRole);
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        return await LoginAsync(client, email);
    }

    private static async Task<Guid> VerifySellerAsync(
        SellerPayoutProfileChangeTestFactory factory,
        Guid userId,
        string payoutProviderReference)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(seller => seller.UserId == userId);
        seller.UpdateProfile(
            "Verified Seller",
            "verified-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Verified Seller Pty");
        var storefront = new SellerStorefront(seller.Id, "Verified Seller", $"verified-{Guid.NewGuid():N}");
        storefront.Publish();
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payoutProfile = new SellerPayoutProfilePlaceholder(seller.Id, payoutProviderReference);
        payoutProfile.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.Parse("2026-05-21T10:00:00Z"));
        seller.MarkVerified(storefront, address, payoutProfile);

        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payoutProfile);
        await dbContext.SaveChangesAsync();
        return seller.Id;
    }

    private static async Task<Guid> SeedPayoutAsync(
        SellerPayoutProfileChangeTestFactory factory,
        Guid sellerId,
        decimal amount)
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
            DateTimeOffset.Parse("2026-05-21T10:00:00Z"));
        var payout = new SellerPayout(sellerId, amount, "ZAR", DateTimeOffset.Parse("2026-05-21T10:00:00Z"));
        payout.AddItem(ledgerEntry.Id, null, null, amount, DateTimeOffset.Parse("2026-05-21T10:00:00Z"));

        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return payout.Id;
    }

    private static async Task<string> CreateAndLoginFinanceUserAsync(
        SellerPayoutProfileChangeTestFactory factory,
        HttpClient client,
        string role)
    {
        var email = $"finance-payout-change-{Guid.NewGuid():N}@example.test";
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

        return (await LoginAsync(client, email)).AccessToken;
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!;
    }

    private sealed class SellerPayoutProfileChangeTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyPayoutProfileChangeTests_{Guid.NewGuid():N}";

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
