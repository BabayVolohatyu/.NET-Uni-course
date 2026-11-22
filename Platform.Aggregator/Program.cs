using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Clients;
using Platform.ServiceDefaults.Auth;
using Platform.Aggregator.Caching;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Resilience;
using System.Text.Json.Serialization;

// ── Platform.Aggregator — Aggregator Pattern implementation ───────────────────
// Receives composite requests → queries multiple microservices in parallel (Task.WhenAll)
// → assembles a single response with partial-failure tolerance (Warnings list).
//
// Lab 06 upgrade:
//   • HTTP typed clients replaced by gRPC clients (lower latency, multiplexed HTTP/2)
//   • Redis distributed cache added for aggregated responses (short TTL = 30 s)
//   • Cache checked BEFORE parallel gRPC calls; partial results not cached
//   • CorrelationId propagated via gRPC metadata headers

var builder = WebApplication.CreateBuilder(args);

// Shared platform defaults: Serilog, OpenTelemetry, CorrelationId, health checks,
// service discovery, resilient HttpClient (all from ServiceDefaults)
builder.AddServiceDefaults();

// ── Lab 06: Redis distributed cache (Aspire integration) ─────────────────────
// Aspire injects ConnectionStrings:redis via WithReference(redis) in AppHost.
// Lab 07: Aspire's own Redis health check is disabled. AggregatorHealthChecks
// registers redis-cache with failure status Degraded — the cache is optional.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);

// ── Lab 06: Aggregator cache metrics ─────────────────────────────────────────
builder.Services.AddSingleton<AggregatorCacheMetrics>();

// ── Lab 07: dossier metrics (counter, histogram, gauge) ──────────────────────
builder.Services.AddSingleton<AggregationMetrics>();

// ── Lab 06 + 07: gRPC clients ─────────────────────────────────────────────────
// Service discovery resolves "https+http://<service-name>".
// Lab 07 replaces the ServiceDefaults handler (RemoveAllResilienceHandlers, then
// one AddStandardResilienceHandler) and adds a gRPC RetryPolicy per client.
// Critical: catalog-api, tenders-api. Non-critical: risk-assessments-api.
builder.AddAggregatorResilience();

// ── Lab 07: readiness — Redis Degraded, downstream gRPC health protocol ──────
builder.AddAggregatorHealthChecks();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// JSON: camelCase + null omission (DTO fields with null not serialized)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Procurement Risk Platform — Aggregator",
        Version     = "v1",
        Description = "Aggregates data from Tenders.Api, Catalog.Api, and RiskAssessments.WebApi. " +
                      "Lab 06: microservice calls now use gRPC (HTTP/2) for lower latency. " +
                      "Aggregated responses are cached in Redis (30 s TTL)."
    });
});

builder.Services.AddProblemDetails();

// ── Lab 09: user JWTs on the dossier API, plus a client-credentials token for downstream calls ──
builder.Services.AddProcurementJwt(
    builder.Configuration,
    identityServerAudience: "orders_api",
    validateIdentityServerAudience: false);
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "DevRealmDrivers", options =>
    {
        options.Audience = "orders-api";
        options.TokenValidationParameters.ValidateAudience = false;
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IdentityServerTokenClient>();
builder.Services.AddHttpClient("tenders-http", client => client.BaseAddress = new Uri("https+http://tenders-api"));
builder.Services.AddHttpClient("catalog-http", client => client.BaseAddress = new Uri("https+http://catalog-api"));
builder.Services.AddHttpClient("identityserver-token");

var app = builder.Build();

// Circuit-breaker transitions log through this logger (OnOpened = Error, OnClosed = Information).
ResilienceLog.Use(app.Logger);

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aggregator v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(o => o.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Title = "An unexpected error occurred",
        Status = StatusCodes.Status500InternalServerError
    });
}));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
