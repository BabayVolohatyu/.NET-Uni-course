using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

// ── API Gateway — single entry point for all clients ──────────────────────────
// Routes all requests to microservices via YARP reverse proxy.
// Responsibilities:
//   • Generate / propagate X-Correlation-Id (CorrelationIdMiddleware from ServiceDefaults)
//   • Route requests to tenders-api, catalog-api, risk-assessments-api, aggregator via YARP
//   • Log every routing decision with method, path, route, status, and duration
//   • Apply ServiceDefaults: Serilog, OpenTelemetry, health checks, service discovery

var builder = WebApplication.CreateBuilder(args);

// Apply cross-cutting defaults: Serilog, OTel, health checks, service discovery, resilience
builder.AddServiceDefaults();

// YARP: load route/cluster config from appsettings.json (ReverseProxy section)
// AddServiceDiscoveryDestinationResolver: resolves "http://tenders-api" → real address via Aspire
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────
// CorrelationId MUST be registered before MapReverseProxy so the header is set
// on the incoming request before YARP copies it to the proxied request.
app.UseMiddleware<CorrelationIdMiddleware>();

// Gateway routing-decision logger middleware
// Runs after YARP sets the route feature; logs method, path, route, status, duration.
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next(context);
    sw.Stop();

    var route = context.GetReverseProxyFeature()?.Route.Config.RouteId ?? "unknown";
    app.Logger.LogInformation(
        "Gateway routed {Method} {Path} → {Route}: {StatusCode} in {ElapsedMs} ms CorrelationId={CorrelationId}",
        context.Request.Method,
        context.Request.Path,
        route,
        context.Response.StatusCode,
        sw.ElapsedMilliseconds,
        context.Request.Headers["X-Correlation-Id"].ToString());
});

// Map YARP proxy routes (defined in appsettings.json)
app.MapReverseProxy();

// /health and /alive endpoints (provided by ServiceDefaults)
app.MapDefaultEndpoints();

app.Run();
