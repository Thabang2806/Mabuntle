using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public class AuthenticationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Buyer_CanRegisterAndLogin()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var registerResponse = await RegisterAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        Assert.Equal(SwyftlyRoles.Buyer, registerResponse.Role);
        Assert.Null(registerResponse.SellerVerificationStatus);
        Assert.Contains(SwyftlyRoles.Buyer, loginResponse.Roles);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.RefreshToken));
    }

    [Fact]
    public async Task Seller_CanRegisterAndLogin_WithPendingVerification()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var registerResponse = await RegisterAsync(client, "seller@example.test", SwyftlyRoles.Seller);
        var loginResponse = await LoginAsync(client, "seller@example.test");

        Assert.Equal(SwyftlyRoles.Seller, registerResponse.Role);
        Assert.Equal("PendingVerification", registerResponse.SellerVerificationStatus);
        Assert.Contains(SwyftlyRoles.Seller, loginResponse.Roles);
    }

    [Fact]
    public async Task Register_RejectsAdminRole()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("admin@example.test", TestPassword, SwyftlyRoles.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminPolicy_RejectsBuyer()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/policy-checks/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SellerPolicy_RejectsBuyer()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/policy-checks/seller");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        response.EnsureSuccessStatusCode();
        var refreshResponse = await ReadJsonAsync<AuthResponse>(response);

        Assert.Contains(SwyftlyRoles.Buyer, refreshResponse.Roles);
        Assert.NotEqual(loginResponse.RefreshToken, refreshResponse.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshResponse.AccessToken));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var logoutResponse = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(loginResponse.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private const string TestPassword = "Password123";

    private static async Task<RegisterResponse> RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<RegisterResponse>(response);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<AuthResponse>(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {content}");
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Response body could not be deserialized as {typeof(T).Name}.");
    }

    private sealed class AuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();
                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }
}
