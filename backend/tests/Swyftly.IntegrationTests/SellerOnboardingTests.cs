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
using Swyftly.Api.Sellers;
using Swyftly.Application.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public class SellerOnboardingTests
{
    private const string TestPassword = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Buyer_CannotAccessSellerOnboarding()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var auth = await RegisterAndLoginAsync(client, "buyer@example.test", SwyftlyRoles.Buyer);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/seller/onboarding");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CanUpdateProfileAndReadOnlyTheirOnboardingState()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var sellerOne = await RegisterAndLoginAsync(client, "seller-one@example.test", SwyftlyRoles.Seller);
        var sellerTwo = await RegisterAndLoginAsync(client, "seller-two@example.test", SwyftlyRoles.Seller);

        await PutAsSellerAsync(
            client,
            sellerOne.AccessToken,
            "/api/seller/onboarding/profile",
            new UpdateSellerProfileRequest(
                "Seller One",
                "seller-one@example.test",
                "+27110000001",
                "Individual",
                null));

        var sellerOneState = await GetOnboardingAsync(client, sellerOne.AccessToken);
        var sellerTwoState = await GetOnboardingAsync(client, sellerTwo.AccessToken);

        Assert.Equal("Seller One", sellerOneState.Profile.DisplayName);
        Assert.Null(sellerTwoState.Profile.DisplayName);
    }

    [Fact]
    public async Task SubmitVerification_RejectsIncompleteOnboarding()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller@example.test", SwyftlyRoles.Seller);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/seller/onboarding/submit-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Complete seller profile", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StorefrontSlug_MustBeUnique()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var sellerOne = await RegisterAndLoginAsync(client, "seller-one@example.test", SwyftlyRoles.Seller);
        var sellerTwo = await RegisterAndLoginAsync(client, "seller-two@example.test", SwyftlyRoles.Seller);

        await PutAsSellerAsync(
            client,
            sellerOne.AccessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest("Seller One", "shared-slug", null, null, null));

        using var response = await PutAsSellerAsync(
            client,
            sellerTwo.AccessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest("Seller Two", "shared-slug", null, null, null),
            ensureSuccess: false);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CompleteSeller_CanSubmitForVerification()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller@example.test", SwyftlyRoles.Seller);
        await CompleteRequiredOnboardingAsync(client, seller.AccessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/seller/onboarding/submit-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.SendAsync(request);

        await EnsureSuccessAsync(response);
        var onboarding = await ReadJsonAsync<SellerOnboardingResponse>(response);

        Assert.Equal("UnderReview", onboarding.VerificationStatus);
        Assert.True(onboarding.CanSubmitForVerification);
    }

    private static async Task CompleteRequiredOnboardingAsync(HttpClient client, string accessToken)
    {
        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/profile",
            new UpdateSellerProfileRequest(
                "Seller Store",
                "seller@example.test",
                "+27110000000",
                "RegisteredBusiness",
                "Seller Trading"));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest(
                "Seller Store",
                "seller-store",
                "Seller storefront",
                null,
                null));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/address",
            new UpdateSellerAddressRequest(
                "1 Market Street",
                null,
                "Johannesburg",
                "Gauteng",
                "2000",
                "ZA"));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/payout",
            new UpdateSellerPayoutRequest("provider-ref-123"));
    }

    private static async Task<SellerOnboardingResponse> GetOnboardingAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/seller/onboarding");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<SellerOnboardingResponse>(response);
    }

    private static async Task<HttpResponseMessage> PutAsSellerAsync<T>(
        HttpClient client,
        string accessToken,
        string uri,
        T requestBody,
        bool ensureSuccess = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        if (ensureSuccess)
        {
            await EnsureSuccessAsync(response);
        }

        return response;
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        await EnsureSuccessAsync(registerResponse);

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        await EnsureSuccessAsync(loginResponse);

        return await ReadJsonAsync<AuthResponse>(loginResponse);
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

    private sealed class SellerOnboardingTestFactory : WebApplicationFactory<Program>
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
