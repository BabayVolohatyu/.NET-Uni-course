using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Asks a downstream gRPC service whether it is SERVING, using the standard
/// grpc.health.v1.Health protocol. The call is idempotent and has no side effects.
/// A 2 second deadline keeps a dead dependency from blocking readiness.
/// </summary>
public sealed class DownstreamGrpcHealthCheck(
    GrpcClientFactory clients,
    ILogger<DownstreamGrpcHealthCheck> logger,
    string serviceName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = clients.CreateClient<Health.HealthClient>(serviceName);
        try
        {
            var reply = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: cancellationToken);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{serviceName} is serving")
                : HealthCheckResult.Unhealthy($"{serviceName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} gRPC health check failed", serviceName);
            return HealthCheckResult.Unhealthy($"{serviceName} is unavailable", ex);
        }
    }
}
