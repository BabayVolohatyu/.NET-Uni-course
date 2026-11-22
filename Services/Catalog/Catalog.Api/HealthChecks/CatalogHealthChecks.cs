using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.HealthChecks;

/// <summary>
/// ContractorRegistry SQL Server is required. Redis is optional and reports Degraded.
/// </summary>
public static class CatalogHealthChecks
{
    public static void AddCatalogHealthChecks(this IHostApplicationBuilder builder, string sqlConnectionString)
    {
        var checks = builder.Services.AddHealthChecks();

        checks.AddSqlServer(
            connectionString: sqlConnectionString,
            healthQuery: "SELECT 1;",
            name: "catalog-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "sql", "ready"],
            timeout: TimeSpan.FromSeconds(5));

        var redis = builder.Configuration.GetConnectionString("redis")
            ?? "localhost:6379,abortConnect=false,connectTimeout=500";
        checks.AddRedis(
            redisConnectionString: redis,
            name: "redis-cache",
            failureStatus: HealthStatus.Degraded,
            tags: ["cache", "redis", "ready"],
            timeout: TimeSpan.FromSeconds(3));
    }
}
