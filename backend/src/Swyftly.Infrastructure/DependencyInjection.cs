using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using Swyftly.Application.Abstractions;
using Swyftly.Application.Advertising;
using Swyftly.Application.Admin;
using Swyftly.Application.Ai;
using Swyftly.Application.Catalog;
using Swyftly.Application.Disputes;
using Swyftly.Application.Inventory;
using Swyftly.Application.Ledger;
using Swyftly.Application.Orders;
using Swyftly.Application.Payments;
using Swyftly.Application.Refunds;
using Swyftly.Application.Returns;
using Swyftly.Application.Search;
using Swyftly.Infrastructure.Admin;
using Swyftly.Infrastructure.Advertising;
using Swyftly.Infrastructure.Ai;
using Swyftly.Infrastructure.Disputes;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Inventory;
using Swyftly.Infrastructure.Ledger;
using Swyftly.Infrastructure.Orders;
using Swyftly.Infrastructure.Payments;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Refunds;
using Swyftly.Infrastructure.Returns;
using Swyftly.Infrastructure.Search;
using Swyftly.Infrastructure.Storage;

namespace Swyftly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionName);
            options.Issuer = section["Issuer"] ?? options.Issuer;
            options.Audience = section["Audience"] ?? options.Audience;
            options.SigningKey = section["SigningKey"] ?? options.SigningKey;

            if (int.TryParse(section["AccessTokenMinutes"], out var accessTokenMinutes))
            {
                options.AccessTokenMinutes = accessTokenMinutes;
            }

            if (int.TryParse(section["RefreshTokenDays"], out var refreshTokenDays))
            {
                options.RefreshTokenDays = refreshTokenDays;
            }
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=swyftly;Username=swyftly;Password=swyftly_dev_password";

        services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SwyftlyDbContext>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuditLogService, EfAuditLogService>();
        services.AddScoped<IProductSearchIndexer, ProductSearchIndexer>();
        services.AddSingleton<ISearchIndexService, LocalSearchIndexService>();
        services.AddSingleton<IImageStorageProvider, DevelopmentImageStorageProvider>();
        services.AddSingleton<ProductModerationService>();
        services.AddSingleton<AiPromptBuilder>();
        services.AddSingleton<AiSuggestionValidator>();
        services.AddScoped<AiUsageLogger>();
        services.AddScoped<IAiListingAssistantService, AiListingAssistantService>();
        services.AddSingleton<IAiProviderClient, FakeAiProviderClient>();
        services.AddScoped<IAiShoppingIntentService, AiShoppingIntentService>();
        services.AddSingleton<IAiShoppingIntentProvider, FakeAiShoppingIntentProvider>();
        services.AddScoped<IAiVisualSearchService, AiVisualSearchService>();
        services.AddSingleton<IAiVisionProvider, FakeAiVisionProvider>();
        services.AddScoped<IProductEmbeddingGenerator, ProductEmbeddingGenerator>();
        services.AddSingleton<IAiEmbeddingService, FakeAiEmbeddingService>();
        services.AddScoped<IInventoryReservationService, EfInventoryReservationService>();
        services.AddScoped<IOrderCreationService, EfOrderCreationService>();
        services.AddScoped<IOrderFulfillmentService, EfOrderFulfillmentService>();
        services.AddScoped<IReturnWorkflowService, EfReturnWorkflowService>();
        services.AddScoped<IRefundWorkflowService, EfRefundWorkflowService>();
        services.AddScoped<IDisputeWorkflowService, EfDisputeWorkflowService>();
        services.AddScoped<IAdCampaignEligibilityService, AdCampaignEligibilityService>();
        services.AddScoped<IAdTrackingService, EfAdTrackingService>();
        services.Configure<PaymentProviderOptions>(options =>
        {
            var section = configuration.GetSection(PaymentProviderOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            options.DefaultCurrency = section["DefaultCurrency"] ?? options.DefaultCurrency;
            options.SuccessRedirectUrl = section["SuccessRedirectUrl"] ?? options.SuccessRedirectUrl;
            options.FailureRedirectUrl = section["FailureRedirectUrl"] ?? options.FailureRedirectUrl;
            options.WebhookSigningSecret = section["WebhookSigningSecret"] ?? options.WebhookSigningSecret;
            options.FakeOutcome = section["FakeOutcome"] ?? options.FakeOutcome;
        });
        services.Configure<LedgerOptions>(options =>
        {
            var section = configuration.GetSection(LedgerOptions.SectionName);
            if (decimal.TryParse(section["PlatformCommissionRatePercent"], out var commissionRate))
            {
                options.PlatformCommissionRatePercent = commissionRate;
            }

            if (decimal.TryParse(section["PaymentProviderFeeRatePercent"], out var providerFeeRate))
            {
                options.PaymentProviderFeeRatePercent = providerFeeRate;
            }

            if (decimal.TryParse(section["PaymentProviderFixedFee"], out var providerFixedFee))
            {
                options.PaymentProviderFixedFee = providerFixedFee;
            }
        });
        services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        services.AddScoped<IPaymentInitiationService, PaymentInitiationService>();
        services.AddScoped<IPaymentService, EfPaymentService>();
        services.AddScoped<ILedgerService, EfLedgerService>();
        services.AddScoped<IPayoutAdministrationService, EfPayoutAdministrationService>();

        return services;
    }
}
