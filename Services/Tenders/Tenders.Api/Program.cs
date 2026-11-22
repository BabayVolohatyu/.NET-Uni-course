using Microsoft.Extensions.Hosting;
using Platform.EventBus;
using Platform.ServiceDefaults.Auth;
using Tenders.Api.IntegrationEvents;
using Serilog;
using Tenders.Api.Grpc;
using Tenders.Api.HealthChecks;
using Tenders.Api.Metrics;
using Tenders.Api.Middleware;
using Tenders.Bll.Caching;
using Tenders.Bll.Interfaces;
using Tenders.Bll.Mapping;
using Tenders.Bll.Services;
using Tenders.Dal;
using Tenders.Dal.Interfaces;

// ── Bootstrap ──────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// ── Lab 05: Platform ServiceDefaults ──────────────────────────────────────────
// Configures Serilog (structured JSON logging with enrichers), OpenTelemetry
// (tracing + metrics for ASP.NET Core, HttpClient, SQL Server), CorrelationId,
// health checks, service discovery, and resilient HttpClient defaults.
// ⚠️ Must come before any other configuration that depends on ILogger.
builder.AddServiceDefaults();

// Lab 08: Aspire.RabbitMQ.Client registers a singleton IConnection, health check, and telemetry.
builder.AddRabbitMQClient("messaging");
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddHostedService<MessagingTopologyHostedService>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddScoped<LocalContractorSync>();
builder.Services.AddScoped<ContractorRenamedHandler>();
builder.Services.AddScoped<ContractorCapitalChangedHandler>();
builder.Services.AddScoped<ContractorRegisteredHandler>();
builder.Services.AddScoped<ContractorRemovedHandler>();

// ── Lab 06: Local in-memory caching ──────────────────────────────────────────
// SizeLimit=10_000 prevents unbounded memory growth.
// Each cache entry has Size=1 set in the caching service.
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

// ── Lab 06: Distributed Redis cache (Aspire integration) ─────────────────────
// Aspire injects ConnectionStrings:redis automatically via WithReference(redis).
// Lab 07 disables Aspire's built-in Redis health check (it would report Unhealthy).
// TenderHealthChecks registers redis-cache as Degraded instead.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);

// ── Lab 06: Cache metrics (OpenTelemetry custom counters) ─────────────────────
builder.Services.AddSingleton<CacheMetrics>();

// ── Lab 07: business counters, histograms, gauges ────────────────────────────
builder.Services.AddSingleton<TenderMetrics>();

// ── Connection string ──────────────────────────────────────────────────────────
// When running under Aspire (AppHost), Aspire injects ConnectionStrings:ProcurementBidding
// automatically via environment variable. The appsettings.json value is a fallback for
// running the service standalone (without AppHost).
var connectionString = builder.Configuration.GetConnectionString("ProcurementBidding")
    ?? throw new InvalidOperationException(
        "Connection string 'ProcurementBidding' is missing from configuration. " +
        "Set it in appsettings.json or via ConnectionStrings__ProcurementBidding env var.");

// ── Dependency injection ───────────────────────────────────────────────────────
// UnitOfWork: Scoped — one connection per HTTP request
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddSingleton(new BiddingMessagingSchema(connectionString));

// BLL services: Scoped (they depend on IUnitOfWork which is Scoped)
// Lab 06: TenderCacheService decorates TenderService with two-level caching.
// Factory registration ensures the decorator receives the concrete TenderService, not itself.
builder.Services.AddScoped<TenderService>();
builder.Services.AddScoped<ITenderService>(sp => new TenderCacheService(
    sp.GetRequiredService<TenderService>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
    sp.GetRequiredService<ILogger<TenderCacheService>>(),
    sp.GetRequiredService<CacheMetrics>()));
builder.Services.AddScoped<IBidService, BidService>();

// AutoMapper: profiles are defined centrally in BLL (not in Api)
builder.Services.AddAutoMapper(cfg =>
{
    // LicenseKey from config — empty string for community/educational use (logs a warning but works)
    cfg.AddProfile<TenderProfile>();
    cfg.AddProfile<BidProfile>();
});

// ── Lab 06: gRPC server ───────────────────────────────────────────────────────
// AddGrpc must be called before AddControllers.
// LoggingInterceptor (in Tenders.Api.Grpc) logs every RPC call with method, peer, status, elapsed.
builder.Services.AddGrpc(o => o.Interceptors.Add<Tenders.Api.Grpc.LoggingInterceptor>());

// ── Lab 07: standard gRPC health protocol + SQL/Redis checks ─────────────────
builder.Services.AddGrpcHealthChecks();
builder.AddTenderHealthChecks(connectionString);

// ── Controllers ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Swagger ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Tenders API",
        Version     = "v1",
        Description = "Public procurement anomaly detection — Tender, Bid, and Procurement Entity management."
    });
});

// ── ProblemDetails + centralized exception handling ────────────────────────────
builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["instance"] = ctx.HttpContext.Request.Path;
    });
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ── Lab 09: Auth Service JWT, IdentityServer (orders_api), Keycloak realm DevRealmDrivers ──
builder.Services.AddProcurementJwt(builder.Configuration, identityServerAudience: "orders_api");
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "DevRealmDrivers", options =>
    {
        options.Audience = "orders-api";
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });
builder.Services.AddPermissionAndScopePolicies(
    permissions: ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"],
    scopes: ["orders.read", "orders.write"]);

// ── Build ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

await app.Services.GetRequiredService<BiddingMessagingSchema>().EnsureAsync();

// ── Middleware pipeline ────────────────────────────────────────────────────────
app.UseExceptionHandler();   // activates DomainExceptionHandler for all unhandled exceptions

// Lab 05: propagate X-Correlation-Id from Gateway to all log records in this service
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenders API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Lab 06: gRPC endpoints ────────────────────────────────────────────────────
// gRPC runs over HTTP/2 (ALPN-negotiated when HTTPS is active).
// MapGrpcService must come after UseRouting (implicit in .NET 8+).
app.MapGrpcService<TendersGrpcService>();

// Lab 07: grpc.health.v1.Health reports the same ASP.NET Core checks.
// MapDefaultEndpoints publishes /health, /health/live, /health/ready, /alive
// in every environment (not only Development).
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
