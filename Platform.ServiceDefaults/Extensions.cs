using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Diagnostics;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Platform-wide default configuration applied to every microservice, Gateway, and Aggregator.
/// Provides: Serilog structured logging, OpenTelemetry tracing + metrics, CorrelationId,
/// health checks, service discovery, and resilient HttpClient defaults.
///
/// Usage in every service's Program.cs:
///   builder.AddServiceDefaults();        // ← after WebApplication.CreateBuilder
///   app.MapDefaultEndpoints();           // ← after app.Build()
///   app.UseMiddleware&lt;CorrelationIdMiddleware&gt;();  // ← before MapControllers / MapReverseProxy
/// </summary>
public static class Extensions
{
    // ── Entry point ──────────────────────────────────────────────────────────────
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // 1. Serilog — structured logging with enrichers, forwarded to OTel providers
        builder.Services.AddSerilog((services, cfg) => cfg
            .MinimumLevel.Is(builder.Environment.IsDevelopment()
                ? LogEventLevel.Debug
                : LogEventLevel.Information)
            // Suppress noisy infrastructure logs
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
            // Enrichers: properties added to every log record
            .Enrich.FromLogContext()                                                   // LogContext.PushProperty(...)
            .Enrich.WithProperty("ServiceName", builder.Environment.ApplicationName) // e.g. "tenders-api"
            .Enrich.WithEnvironmentName()                                             // Development / Production
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            // Console sink: CompactJsonFormatter produces JSON — visible in Aspire Dashboard "Console logs"
            .WriteTo.Console(new CompactJsonFormatter()),
            // ⚠️ writeToProviders: true is REQUIRED so Serilog records also flow through the
            // standard ILogger pipeline → OpenTelemetry logging provider → OTLP → Dashboard "Structured logs"
            writeToProviders: true);

        // 2. OpenTelemetry — distributed tracing and metrics
        builder.ConfigureOpenTelemetry();

        // 3. Health checks (Aspire Dashboard reads /health and /alive)
        builder.AddDefaultHealthChecks();

        // 4. Service discovery — resolves "https+http://tenders-api" to actual address
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();

        // 5. CorrelationId DelegatingHandler — propagates X-Correlation-Id to outgoing requests
        builder.Services.AddTransient<CorrelationIdHandler>();

        // 6. HttpClient defaults — resilience + service discovery + CorrelationId on every client
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();     // retry, circuit breaker, timeout (Polly-based)
            http.AddServiceDiscovery();              // resolves logical service names
            http.AddHttpMessageHandler<CorrelationIdHandler>(); // propagates X-Correlation-Id
        });

        return builder;
    }

    // ── OpenTelemetry configuration ──────────────────────────────────────────────
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // OpenTelemetry logging provider: receives Serilog records (writeToProviders: true) → OTLP
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // Lab 06: custom cache hit/miss metrics — one meter per service
                .AddMeter("Tenders.Cache")
                .AddMeter("Catalog.Cache")
                .AddMeter("RiskAssessments.Cache")
                .AddMeter("Platform.Aggregator.Cache"))
            .WithTracing(tracing => tracing
                // Own spans from each service (created with ActivitySource)
                .AddSource(builder.Environment.ApplicationName)
                // MongoDB Driver spans (Lab 04 — RiskAssessments)
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                // Instrumentation for incoming HTTP requests (ASP.NET Core) — also covers gRPC server spans
                .AddAspNetCoreInstrumentation()
                // Instrumentation for outgoing HTTP calls (HttpClient) — also covers gRPC client spans
                .AddHttpClientInstrumentation()
                // SQL Server instrumentation: covers both Dapper (Lab 02) and EF Core (Lab 03)
                // Note: SqlClient instrumentation options vary by package version;
                // db.statement is still captured for stored procedures by default.
                .AddSqlClientInstrumentation());

        // Export to Aspire Dashboard via OTLP (address is auto-injected by AppHost)
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    // ── Health checks ─────────────────────────────────────────────────────────────
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    // ── Map /health and /alive endpoints ─────────────────────────────────────────
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // /health — full readiness check (all registered checks)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true
        });

        // /alive — liveness check (self only — does not check external dependencies)
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}

// ── CorrelationId middleware ──────────────────────────────────────────────────────
/// <summary>
/// Creates or propagates the X-Correlation-Id header throughout the request lifecycle.
///
/// Behaviour:
///   • If the incoming request already has X-Correlation-Id — reuse it (client or upstream service set it).
///   • Otherwise — generate a new GUID (this is the system entry point, e.g. Gateway).
///   • Push CorrelationId into Serilog LogContext → all log records within this request include it.
///   • Return the same CorrelationId in the response header for client-side correlation.
///
/// CorrelationId vs TraceId:
///   CorrelationId is a business-level identifier created by the application.
///   TraceId is a technical identifier managed automatically by OpenTelemetry's W3C Trace Context.
///   Both are needed: CorrelationId for business audits, TraceId for performance analysis.
/// </summary>
public class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract existing or generate a new CorrelationId
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;
            // Log the generation event for audit trails in procurement systems
            logger.LogDebug("Generated new CorrelationId {CorrelationId} for {Method} {Path}",
                correlationId.ToString(), context.Request.Method, context.Request.Path);
        }

        // Expose in response so clients can reference it in support tickets
        context.Response.Headers[HeaderName] = correlationId;

        // Push to Serilog LogContext — all log records within this request carry CorrelationId
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            logger.LogInformation(
                "Request started {Method} {Path} CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path, correlationId.ToString());

            await next(context);

            logger.LogInformation(
                "Request finished {Method} {Path} StatusCode={StatusCode} CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, correlationId.ToString());
        }
    }
}

// ── CorrelationId DelegatingHandler ──────────────────────────────────────────────
/// <summary>
/// Propagates X-Correlation-Id from the current HTTP context to all outgoing HttpClient requests.
/// Registered via ConfigureHttpClientDefaults → all typed clients in Aggregator get it automatically.
///
/// Flow: Gateway generates CorrelationId → CorrelationIdMiddleware writes it to Request.Headers
///   → CorrelationIdHandler reads it from IHttpContextAccessor → adds to outgoing request headers
///   → downstream service (Tenders.Api / Catalog.Api / RiskAssessments.WebApi) sees same CorrelationId.
/// </summary>
public class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = accessor.HttpContext?
            .Request.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString();

        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(
                CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
