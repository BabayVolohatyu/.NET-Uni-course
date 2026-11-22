using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.WebApi.HealthChecks;

/// <summary>
/// MongoDB ping is required (Unhealthy). Redis is optional (Degraded): queries fall
/// through to the riskAssessments collection when the cache is down.
/// </summary>
public static class RiskAssessmentHealthChecks
{
    public static void AddRiskAssessmentHealthChecks(this IHostApplicationBuilder builder)
    {
        var checks = builder.Services.AddHealthChecks();

        checks.AddMongoDb(
            clientFactory: sp => sp.GetRequiredService<MongoDbContext>().Client,
            databaseNameFactory: sp => sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.DatabaseName,
            name: "risk-assessments-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "mongodb", "ready"],
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
