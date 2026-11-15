using Catalog.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Caching;
using RiskAssessments.Grpc;
using System.Text.Json.Serialization;
using Tenders.Grpc;

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
builder.AddRedisDistributedCache("redis");

// ── Lab 06: Aggregator cache metrics ─────────────────────────────────────────
builder.Services.AddSingleton<AggregatorCacheMetrics>();

// ── Lab 06: gRPC clients ──────────────────────────────────────────────────────
// AddGrpcClient uses the same HttpClient factory as the previous HTTP clients.
// Service discovery resolves "https+http://<service-name>" to the actual address.
// CorrelationIdHandler (registered in ServiceDefaults.ConfigureHttpClientDefaults)
// adds X-Correlation-Id to outgoing gRPC metadata automatically.
builder.Services.AddGrpcClient<TendersService.TendersServiceClient>(o =>
    o.Address = new Uri("https+http://tenders-api"));

builder.Services.AddGrpcClient<ContractorService.ContractorServiceClient>(o =>
    o.Address = new Uri("https+http://catalog-api"));

builder.Services.AddGrpcClient<RiskAssessmentsService.RiskAssessmentsServiceClient>(o =>
    o.Address = new Uri("https+http://risk-assessments-api"));

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

var app = builder.Build();

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

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
