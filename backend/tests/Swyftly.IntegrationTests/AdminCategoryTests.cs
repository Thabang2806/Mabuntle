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
using Swyftly.Api.Catalog;
using Swyftly.Application.Identity;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AdminCategoryTests
{
    [Fact]
    public async Task Buyer_CannotListAdminCategories()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/categories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListSeededCategoriesAndAttributes()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/categories");

        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<AdminCategoryResponse[]>();
        Assert.NotNull(categories);

        var dresses = Assert.Single(categories!, category => category.Slug == "women-clothing-dresses");
        Assert.Equal(CatalogSeedData.WomenClothing, dresses.ParentCategoryId);
        Assert.Contains(dresses.Attributes, attribute =>
            attribute.Key == "size"
            && attribute.DataType == "Select"
            && attribute.IsRequired
            && attribute.AllowedValues.Contains("M"));
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "category-buyer@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminCategoryTestFactory factory,
        HttpClient client)
    {
        const string email = "category-admin@example.test";

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

    private sealed class AdminCategoryTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAdminCategoryTests_{Guid.NewGuid():N}";

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
