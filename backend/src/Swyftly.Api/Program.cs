using Microsoft.Extensions.Diagnostics.HealthChecks;
using Swyftly.Infrastructure;
using Swyftly.Infrastructure.Persistence;

const int ReadinessTimeoutSeconds = 5;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<SwyftlyDbContext>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "database" });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Swyftly API",
        Version = "v1",
        Description = "Foundation API for the Swyftly marketplace."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Swyftly API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("LocalFrontend");

app.MapGet("/health", () => new HealthResponse(
    "Healthy",
    "Swyftly.Api",
    DateTimeOffset.UtcNow))
    .WithName("GetHealth")
    .WithSummary("Returns the API health status.")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/health/ready", async (HealthCheckService healthCheckService) =>
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ReadinessTimeoutSeconds));
    HealthReport report;

    try
    {
        report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            timeout.Token);
    }
    catch (OperationCanceledException)
    {
        var timeoutResponse = new ReadinessHealthResponse(
            HealthStatus.Unhealthy.ToString(),
            "Swyftly.Api",
            DateTimeOffset.UtcNow,
            ReadinessTimeoutSeconds * 1000,
            new Dictionary<string, ReadinessCheckResponse>
            {
                ["postgresql"] = new(
                    HealthStatus.Unhealthy.ToString(),
                    "Readiness check timed out.",
                    null,
                    ReadinessTimeoutSeconds * 1000)
            });

        return Results.Json(timeoutResponse, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var checks = report.Entries.ToDictionary(
        entry => entry.Key,
        entry => new ReadinessCheckResponse(
            entry.Value.Status.ToString(),
            entry.Value.Description,
            entry.Value.Exception?.Message,
            Math.Round(entry.Value.Duration.TotalMilliseconds, 2)));

    var response = new ReadinessHealthResponse(
        report.Status.ToString(),
        "Swyftly.Api",
        DateTimeOffset.UtcNow,
        Math.Round(report.TotalDuration.TotalMilliseconds, 2),
        checks);

    var statusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    return Results.Json(response, statusCode: statusCode);
})
    .WithName("GetReadiness")
    .WithSummary("Returns readiness status for API dependencies.")
    .Produces<ReadinessHealthResponse>(StatusCodes.Status200OK)
    .Produces<ReadinessHealthResponse>(StatusCodes.Status503ServiceUnavailable);

app.Run();

public partial class Program;

public sealed record HealthResponse(
    string Status,
    string ApplicationName,
    DateTimeOffset TimestampUtc);

public sealed record ReadinessHealthResponse(
    string Status,
    string ApplicationName,
    DateTimeOffset TimestampUtc,
    double TotalDurationMilliseconds,
    IReadOnlyDictionary<string, ReadinessCheckResponse> Checks);

public sealed record ReadinessCheckResponse(
    string Status,
    string? Description,
    string? Error,
    double DurationMilliseconds);
