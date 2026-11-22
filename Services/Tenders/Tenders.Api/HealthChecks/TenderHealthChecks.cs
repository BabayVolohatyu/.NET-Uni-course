using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tenders.Api.HealthChecks;

/// <summary>
/// SQL Server is required (Unhealthy). Redis is a cache (Degraded): the service
/// reads ProcurementBidding when the cache is down.
/// The SQL check runs SELECT 1 — nothing heavier, because probes are frequent.
/// </summary>
public static class TenderHealthChecks
{
    public static void AddTenderHealthChecks(this IHostApplicationBuilder builder, string sqlConnectionString)
    {
        var checks = builder.Services.AddHealthChecks();

        checks.AddSqlServer(
            connectionString: sqlConnectionString,
            healthQuery: "SELECT 1;",
            name: "tenders-db",
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
