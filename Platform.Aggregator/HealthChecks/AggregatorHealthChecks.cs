using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Resilience;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Readiness of the Aggregator: Redis is optional (Degraded), the tender and contractor
/// services are required (Unhealthy), and risk assessments are optional (Degraded)
/// because the dossier is still returned with a warning when that service is down.
/// </summary>
public static class AggregatorHealthChecks
{
    public static void AddAggregatorHealthChecks(this IHostApplicationBuilder builder)
    {
        AddHealthClient(builder, "tenders-api", "https+http://tenders-api");
        AddHealthClient(builder, "catalog-api", "https+http://catalog-api");
        AddHealthClient(builder, "risk-assessments-api", "https+http://risk-assessments-api");

        var checks = builder.Services.AddHealthChecks();

        var redis = builder.Configuration.GetConnectionString("redis")
            ?? "localhost:6379,abortConnect=false,connectTimeout=500";
        checks.AddRedis(
            redisConnectionString: redis,
            name: "redis-cache",
            failureStatus: HealthStatus.Degraded,
            tags: ["cache", "redis", "ready"],
            timeout: TimeSpan.FromSeconds(3));

        RegisterDownstream(checks, "tenders-api", HealthStatus.Unhealthy);
        RegisterDownstream(checks, "catalog-api", HealthStatus.Unhealthy);
        // Risk flags are an enrichment. The Aggregator still serves a partial dossier.
        RegisterDownstream(checks, "risk-assessments-api", HealthStatus.Degraded);
    }

    private static void AddHealthClient(IHostApplicationBuilder builder, string name, string address)
    {
        builder.Services.AddGrpcClient<Health.HealthClient>(name, o => o.Address = new Uri(address))
            .ConfigureChannel(channel => channel.ServiceConfig = AggregatorResilience.GrpcRetry(maxAttempts: 2))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options => AggregatorResilience.ApplyHealthProbe(options, name));
    }

    private static void RegisterDownstream(IHealthChecksBuilder checks, string name, HealthStatus failureStatus)
    {
        checks.Add(new HealthCheckRegistration(
            name,
            sp => ActivatorUtilities.CreateInstance<DownstreamGrpcHealthCheck>(sp, name),
            failureStatus,
            ["grpc", "downstream", "ready"],
            TimeSpan.FromSeconds(3)));
    }
}
