namespace Swyftly.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("SWYFTLY_RUN_POSTGRES_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set SWYFTLY_RUN_POSTGRES_TESTS=true to run PostgreSQL integration tests.";
        }
    }
}
