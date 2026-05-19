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
using Swyftly.Api.Support;
using Swyftly.Application.Identity;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class SupportTicketTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanCreateTicket_AndInternalNotesAreHiddenFromBuyer()
    {
        using var factory = new SupportTicketTestFactory();
        using var buyerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-support@example.test", SwyftlyRoles.Buyer);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            "/api/buyer/support-tickets",
            new CreateSupportTicketRequest(
                "OrderIssue",
                "Order arrived damaged",
                "The box arrived damaged.",
                null,
                null,
                null,
                null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);
        Assert.Equal("Open", created!.Status);

        supportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, supportClient, "support-agent@example.test", SwyftlyRoles.SupportAgent));

        using var internalNoteResponse = await supportClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/internal-notes",
            new SupportMessageRequest("Check recent refund history before replying."));
        internalNoteResponse.EnsureSuccessStatusCode();

        using var supportMessageResponse = await supportClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/messages",
            new SupportMessageRequest("Please upload a photo of the damage."));
        supportMessageResponse.EnsureSuccessStatusCode();
        var supportView = await supportMessageResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(supportView);
        Assert.Contains(supportView!.Messages, message => message.IsInternal);
        Assert.Equal("WaitingForCustomer", supportView.Status);

        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerAuth.AccessToken);
        using var buyerViewResponse = await buyerClient.GetAsync($"/api/buyer/support-tickets/{created.SupportTicketId}");
        buyerViewResponse.EnsureSuccessStatusCode();
        var buyerView = await buyerViewResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(buyerView);
        Assert.DoesNotContain(buyerView!.Messages, message => message.IsInternal);
        Assert.Contains(buyerView.Messages, message => message.Message == "Please upload a photo of the damage.");
    }

    [Fact]
    public async Task SellerCanCreateTicket_AndAdminCanRespond()
    {
        using var factory = new SupportTicketTestFactory();
        using var sellerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        await RegisterAndLoginAsync(sellerClient, "seller-support@example.test", SwyftlyRoles.Seller);

        using var createResponse = await sellerClient.PostAsJsonAsync(
            "/api/seller/support-tickets",
            new CreateSupportTicketRequest(
                "PaymentIssue",
                "Payout question",
                "My payout is still pending.",
                null,
                null,
                null,
                null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);
        Assert.Equal("Seller", created!.CreatedByRole);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "admin-support@example.test", SwyftlyRoles.Admin));

        using var response = await adminClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/messages",
            new SupportMessageRequest("The payout is waiting for dispute clearance."));
        response.EnsureSuccessStatusCode();
        var supportView = await response.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(supportView);
        Assert.Equal("WaitingForSeller", supportView!.Status);
        Assert.Contains(supportView.Messages, message => message.SenderRole == "Admin");
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private static async Task<string> CreateAndLoginUserInRoleAsync(
        SupportTicketTestFactory factory,
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

            var createResult = await userManager.CreateAsync(user, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class SupportTicketTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlySupportTicketTests_{Guid.NewGuid():N}";

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
