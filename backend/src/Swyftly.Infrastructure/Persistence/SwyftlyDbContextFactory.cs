using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;
using System.Text.Json;

namespace Swyftly.Infrastructure.Persistence;

public sealed class SwyftlyDbContextFactory : IDesignTimeDbContextFactory<SwyftlyDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=swyftly;Username=swyftly;Password=swyftly_dev_password";

    public SwyftlyDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SWYFTLY_MIGRATIONS_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? TryReadConnectionStringFromApiSettings("appsettings.Development.json")
            ?? TryReadConnectionStringFromApiSettings("appsettings.json")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(TimeProvider.System))
            .Options;

        return new SwyftlyDbContext(options);
    }

    private static string? TryReadConnectionStringFromApiSettings(string fileName)
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(
                currentDirectory.FullName,
                "backend",
                "src",
                "Swyftly.Api",
                fileName);

            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));

                if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                    && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
                {
                    return defaultConnection.GetString();
                }
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }
}
