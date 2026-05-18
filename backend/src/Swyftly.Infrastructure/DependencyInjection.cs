using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=swyftly;Username=swyftly;Password=swyftly_dev_password";

        services.AddDbContext<SwyftlyDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
