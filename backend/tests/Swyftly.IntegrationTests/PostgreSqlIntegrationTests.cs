using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public class PostgreSqlIntegrationTests
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=swyftly_integration_tests;Username=swyftly;Password=swyftly_dev_password";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("SWYFTLY_TEST_POSTGRES_CONNECTION")
        ?? DefaultConnectionString;

    [PostgreSqlFact]
    public async Task PostgreSql_MigrationsCanBeApplied()
    {
        await ApplyMigrationsAsync();

        await using var dbContext = CreateDbContext();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [PostgreSqlFact]
    public async Task PostgreSql_ReadinessEndpointReturnsHealthy()
    {
        await ApplyMigrationsAsync();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString
                    });
                });
            });

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"postgresql\"", content, StringComparison.OrdinalIgnoreCase);
    }

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SwyftlyDbContext(options);
    }

    private static async Task ApplyMigrationsAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }
}
