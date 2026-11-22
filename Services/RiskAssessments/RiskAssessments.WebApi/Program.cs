using Microsoft.Extensions.Hosting;
using Platform.EventBus;
using RiskAssessments.WebApi.IntegrationEvents;
using RiskAssessments.Application;
using RiskAssessments.Application.Caching;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Mapping;
using RiskAssessments.Infrastructure;
using RiskAssessments.Infrastructure.Indexing;
using RiskAssessments.Infrastructure.Persistence;
using RiskAssessments.Infrastructure.Seeding;
using RiskAssessments.WebApi.ExceptionHandling;
using RiskAssessments.WebApi.Grpc;
using RiskAssessments.WebApi.HealthChecks;
using RiskAssessments.WebApi.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Lab 05: Platform ServiceDefaults ──────────────────────────────────────────
// Configures Serilog (structured JSON logging with enrichers), OpenTelemetry
// (tracing + metrics for ASP.NET Core, HttpClient, MongoDB via DiagnosticSources),
// CorrelationId, health checks, service discovery, and resilient HttpClient defaults.
builder.AddServiceDefaults();

// Lab 08: Aspire.RabbitMQ.Client registers a singleton IConnection, health check, and telemetry.
builder.AddRabbitMQClient("messaging");
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddHostedService<MessagingTopologyHostedService>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddScoped<RiskCatalogSync>();
builder.Services.AddScoped<ContractorRenamedHandler>();
builder.Services.AddScoped<ContractorCapitalChangedHandler>();
builder.Services.AddScoped<ContractorRegisteredHandler>();
builder.Services.AddScoped<ContractorRemovedHandler>();

// ── Lab 06: Local in-memory caching ──────────────────────────────────────────
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

// ── Lab 06: Distributed Redis cache (Aspire integration) ─────────────────────
// Lab 07: disable Aspire's Redis health check; ours reports Degraded.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);

// ── Lab 06: Cache metrics (OpenTelemetry custom counters) ─────────────────────
builder.Services.AddSingleton<CacheMetrics>();

// ── Lab 07: business counters, histograms, gauges ────────────────────────────
builder.Services.AddSingleton<RiskAssessmentMetrics>();

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

// ── Lab 06: Wrap IRiskAssessmentQueries with two-level cache decorator ─────────
// Infrastructure registers RiskAssessmentQueries → IRiskAssessmentQueries.
// We replace that binding with the cache decorator by removing the original
// and re-registering via factory. The concrete type is also registered so the
// decorator can receive it via DI.
builder.Services.AddScoped<RiskAssessments.Infrastructure.Persistence.RiskAssessmentQueries>();
var raqDescriptor = builder.Services
    .LastOrDefault(d => d.ServiceType == typeof(IRiskAssessmentQueries)
                     && d.ImplementationType == typeof(RiskAssessments.Infrastructure.Persistence.RiskAssessmentQueries));
if (raqDescriptor is not null) builder.Services.Remove(raqDescriptor);
builder.Services.AddScoped<IRiskAssessmentQueries>(sp => new RiskAssessmentCacheService(
    sp.GetRequiredService<RiskAssessments.Infrastructure.Persistence.RiskAssessmentQueries>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
    sp.GetRequiredService<ILogger<RiskAssessmentCacheService>>(),
    sp.GetRequiredService<CacheMetrics>()));

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"];
    cfg.AddMaps(typeof(RiskAssessmentProfile).Assembly);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Risk Assessment API",
        Version = "v1",
        Description = "Clean Architecture + CQRS over the RiskAssessment MongoDB context. " +
                      "Auditors read tender risk scores, anomaly flags, and audit notes."
    });
});

builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["instance"] = ctx.HttpContext.Request.Path.Value;
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Lab 06: gRPC server ───────────────────────────────────────────────────────
builder.Services.AddGrpc(o => o.Interceptors.Add<RiskAssessments.WebApi.Grpc.LoggingInterceptor>());

// ── Lab 07: gRPC health protocol + MongoDB ping and Redis (Degraded) ─────────
builder.Services.AddGrpcHealthChecks();
builder.AddRiskAssessmentHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var indexes = scope.ServiceProvider.GetRequiredService<IIndexCreationService>();
    await indexes.CreateAsync();

    if (!app.Environment.IsProduction())
    {
        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>())
            await seeder.SeedAsync();
    }
}

app.UseExceptionHandler();

// Lab 05: propagate X-Correlation-Id from Gateway to all log records in this service
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Risk Assessment API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapControllers();

// ── Lab 06: gRPC endpoints ────────────────────────────────────────────────────
app.MapGrpcService<RiskAssessmentsGrpcService>();

// Lab 07: grpc.health.v1.Health plus /health/live and /health/ready (all environments).
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
