using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Catalog.Grpc;
using RiskAssessments.Grpc;
using Tenders.Grpc;

namespace Platform.Aggregator.Resilience;

/// <summary>
/// Per-client resilience for the Aggregator.
///
/// ServiceDefaults already calls AddStandardResilienceHandler inside
/// ConfigureHttpClientDefaults, so every HttpClient — including gRPC clients —
/// already has one standard handler. Customising a client therefore always
/// starts with RemoveAllResilienceHandlers and then adds exactly one handler.
/// Adding a second handler without removing the first would stack pipelines.
///
/// gRPC status codes (Unavailable, ResourceExhausted) travel as HTTP 200 plus
/// trailers, so the HTTP handler does not retry them. Each gRPC channel also
/// gets a ServiceConfig RetryPolicy for those statuses. The HTTP handler still
/// retries transport failures (connection reset, timeout) on the idempotent reads.
///
/// Why these values: see the table in the root README, section "Lab 07".
/// SamplingDuration is at least twice AttemptTimeout, otherwise options validation
/// refuses to start the host. TotalRequestTimeout covers every attempt plus backoff.
/// MinimumThroughput is far below the production default of 100 so a short failure
/// burst on this stand can actually open the breaker.
/// </summary>
public static class AggregatorResilience
{
    public static void AddAggregatorResilience(this IHostApplicationBuilder builder)
    {
        // Critical: contractor registry. The audit dossier cannot name who bid,
        // or explain a shell-company flag, without this service.
        builder.Services.AddGrpcClient<ContractorService.ContractorServiceClient>(o =>
            o.Address = new Uri("https+http://catalog-api"))
            .ConfigureChannel(channel => channel.ServiceConfig = GrpcRetry(maxAttempts: 5))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options => ApplyCriticalCatalog(options));

        // Critical: tender lifecycle. Without the tender there is nothing to aggregate.
        builder.Services.AddGrpcClient<TendersService.TendersServiceClient>(o =>
            o.Address = new Uri("https+http://tenders-api"))
            .ConfigureChannel(channel => channel.ServiceConfig = GrpcRetry(maxAttempts: 4))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options => ApplyCriticalTenders(options));

        // Non-critical: risk scores enrich the dossier. One retry, then the Aggregator
        // returns a partial response (Lab 05 warnings list) instead of failing the call.
        builder.Services.AddGrpcClient<RiskAssessmentsService.RiskAssessmentsServiceClient>(o =>
            o.Address = new Uri("https+http://risk-assessments-api"))
            .ConfigureChannel(channel => channel.ServiceConfig = GrpcRetry(maxAttempts: 2))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options => ApplyNonCriticalRisk(options));

        // Non-idempotent procurement commands (publish a tender, award a bid) must not
        // be replayed. This client is the write policy: retries stay enabled for GET,
        // and DisableForUnsafeHttpMethods turns them off for POST, PUT, and DELETE.
        // The Aggregator itself only reads; the guard documents the write rule the
        // platform uses so a retried award cannot be applied twice.
        builder.Services.AddHttpClient("procurement-commands")
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(ApplyCommandPolicy);
    }

    /// <summary>
    /// Built-in gRPC retry. MaxAttempts includes the original call (gRPC caps it at 5).
    /// </summary>
    public static ServiceConfig GrpcRetry(int maxAttempts) => new()
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = maxAttempts,
                    InitialBackoff = TimeSpan.FromSeconds(0.5),
                    MaxBackoff = TimeSpan.FromSeconds(4),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes =
                    {
                        StatusCode.Unavailable,
                        StatusCode.ResourceExhausted
                    }
                }
            }
        }
    };

    public static void ApplyCriticalCatalog(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 5;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        // 6 attempts × 3s + exponential backoff fits inside 30s.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

        options.CircuitBreaker.FailureRatio = 0.3;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        WireCircuitLogs(options, "catalog-api");
    }

    public static void ApplyCriticalTenders(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 4;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);

        // More sensitive than catalog: a missing tender makes the whole dossier impossible.
        options.CircuitBreaker.FailureRatio = 0.25;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(20);
        WireCircuitLogs(options, "tenders-api");
    }

    public static void ApplyNonCriticalRisk(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        // Give up quickly so the Aggregator can return the tender and contractors alone.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);

        options.CircuitBreaker.FailureRatio = 0.7;
        options.CircuitBreaker.MinimumThroughput = 6;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        WireCircuitLogs(options, "risk-assessments-api");
    }

    public static void ApplyCommandPolicy(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        // Publishing a tender or awarding a bid is not idempotent.
        options.Retry.DisableForUnsafeHttpMethods();

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        WireCircuitLogs(options, "procurement-commands");
    }

    /// <summary>
    /// Short pipeline for gRPC health probes. The check itself also sets a 2s deadline.
    /// </summary>
    public static void ApplyHealthProbe(HttpStandardResilienceOptions options, string clientName)
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.FromMilliseconds(100);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
        WireCircuitLogs(options, clientName);
    }

    private static void WireCircuitLogs(HttpStandardResilienceOptions options, string clientName)
    {
        options.CircuitBreaker.OnOpened = args =>
        {
            ResilienceLog.CircuitOpened(clientName, args.BreakDuration);
            return ValueTask.CompletedTask;
        };
        options.CircuitBreaker.OnClosed = _ =>
        {
            ResilienceLog.CircuitClosed(clientName);
            return ValueTask.CompletedTask;
        };
        options.CircuitBreaker.OnHalfOpened = _ =>
        {
            ResilienceLog.CircuitHalfOpened(clientName);
            return ValueTask.CompletedTask;
        };
    }
}

/// <summary>
/// Circuit-transition logger. The options callback runs before the host is built,
/// so the delegate reads the logger at event time, after <see cref="Use"/> is called.
/// LogContext from CorrelationIdMiddleware still attaches CorrelationId and the
/// ServiceDefaults enricher adds ServiceName on the same record.
/// </summary>
public static class ResilienceLog
{
    private static ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public static void Use(ILogger logger) => _logger = logger;

    public static void CircuitOpened(string client, TimeSpan duration) =>
        _logger.LogError(
            "Circuit opened for {Client} for {Duration}. Further calls fail fast until a trial request is allowed.",
            client, duration);

    public static void CircuitClosed(string client) =>
        _logger.LogInformation("Circuit closed for {Client}. Calls are flowing again.", client);

    public static void CircuitHalfOpened(string client) =>
        _logger.LogWarning("Circuit half-opened for {Client}. One trial call is in flight.", client);
}
