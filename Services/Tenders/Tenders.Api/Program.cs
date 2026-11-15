using Microsoft.Extensions.Hosting;
using Serilog;
using Tenders.Api.Grpc;
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

// ── Lab 06: Local in-memory caching ──────────────────────────────────────────
// SizeLimit=10_000 prevents unbounded memory growth.
// Each cache entry has Size=1 set in the caching service.
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

// ── Lab 06: Distributed Redis cache (Aspire integration) ─────────────────────
// Aspire injects ConnectionStrings:redis automatically via WithReference(redis).
// No need to set the address manually in appsettings.
builder.AddRedisDistributedCache("redis");

// ── Lab 06: Cache metrics (OpenTelemetry custom counters) ─────────────────────
builder.Services.AddSingleton<CacheMetrics>();

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

// ── Build ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

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
app.MapControllers();

// ── Lab 06: gRPC endpoints ────────────────────────────────────────────────────
// gRPC runs over HTTP/2 (ALPN-negotiated when HTTPS is active).
// MapGrpcService must come after UseRouting (implicit in .NET 8+).
app.MapGrpcService<TendersGrpcService>();

// ── Lab 05: Health check endpoints (/health and /alive) ───────────────────────
// These are consumed by Aspire Dashboard to display service readiness status.
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
