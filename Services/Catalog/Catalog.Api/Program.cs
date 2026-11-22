using Catalog.Api.Grpc;
using Platform.ServiceDefaults.Auth;
using Platform.EventBus;
using Catalog.Api.HealthChecks;
using Catalog.Api.Metrics;
using Catalog.Api.Middleware;
using Catalog.Bll.Caching;
using Catalog.Bll.Interfaces;
using Catalog.Bll.MappingProfiles;
using Catalog.Bll.Services;
using Catalog.Bll.Validators;
using Catalog.Dal;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seeding;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

// ── Bootstrap ──────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// ── Lab 05: Platform ServiceDefaults ──────────────────────────────────────────
// Configures Serilog (structured JSON logging with enrichers), OpenTelemetry
// (tracing + metrics for ASP.NET Core, HttpClient, SQL Server via EF Core),
// CorrelationId, health checks, service discovery, and resilient HttpClient defaults.
builder.AddServiceDefaults();

// Lab 08: Aspire.RabbitMQ.Client registers a singleton IConnection, health check, and telemetry.
builder.AddRabbitMQClient("messaging");
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddHostedService<MessagingTopologyHostedService>();

// ── Lab 06: Local in-memory caching ──────────────────────────────────────────
// SizeLimit=10_000 prevents unbounded memory growth (units = cache entry Size values).
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

// ── Lab 06: Distributed Redis cache (Aspire integration) ─────────────────────
// Aspire injects ConnectionStrings:redis automatically via WithReference(redis).
// Lab 07: built-in Redis health check is off so we can report Degraded, not Unhealthy.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);

// ── Lab 06: Cache metrics (OpenTelemetry custom counters) ─────────────────────
builder.Services.AddSingleton<CacheMetrics>();

// ── Lab 07: business counters, histograms, gauges ────────────────────────────
builder.Services.AddSingleton<ContractorMetrics>();

// ── Database: EF Core + SQL Server ────────────────────────────────────────────
// When running under Aspire (AppHost), Aspire injects ConnectionStrings:ContractorRegistry
// automatically via environment variable. The appsettings.json value is a fallback.
var connectionString = builder.Configuration.GetConnectionString("ContractorRegistry")
    ?? throw new InvalidOperationException(
        "Connection string 'ContractorRegistry' is missing. " +
        "Set it in appsettings.json or via ConnectionStrings__ContractorRegistry env var.");

builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseSqlServer(connectionString));

// ── Unit of Work ───────────────────────────────────────────────────────────────
// Scoped to match CatalogDbContext lifetime — one context per HTTP request
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── BLL services ───────────────────────────────────────────────────────────────
// Lab 06: ContractorCacheService decorates ContractorService with two-level caching.
builder.Services.AddScoped<ContractorService>();
builder.Services.AddScoped<IContractorService>(sp => new ContractorCacheService(
    sp.GetRequiredService<ContractorService>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
    sp.GetRequiredService<ILogger<ContractorCacheService>>(),
    sp.GetRequiredService<CacheMetrics>()));
builder.Services.AddScoped<IWorkCategoryService, WorkCategoryService>();

// ── AutoMapper ─────────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(cfg =>
{
    // Community LicenseKey from config (automapper.io — free for educational use)
    cfg.AddProfile<ContractorProfile>();
});

// ── FluentValidation ───────────────────────────────────────────────────────────
// Validators are injected into controllers via [FromServices] and called explicitly —
// NOT via the deprecated AddFluentValidation middleware
builder.Services.AddValidatorsFromAssemblyContaining<CreateContractorDtoValidator>();

// ── Lab 06: gRPC server ───────────────────────────────────────────────────────
builder.Services.AddGrpc(o => o.Interceptors.Add<Catalog.Api.Grpc.LoggingInterceptor>());

// ── Lab 07: gRPC health protocol + SQL/Redis checks ──────────────────────────
builder.Services.AddGrpcHealthChecks();
builder.AddCatalogHealthChecks(connectionString);

// ── Controllers ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Swagger ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Contractor Registry API",
        Version     = "v1",
        Description = "Public procurement contractor registry — anomaly detection and risk assessment platform."
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

// ── Lab 09: Auth Service JWT, IdentityServer (products_api), Keycloak realm DevRealmDrivers ──
builder.Services.AddProcurementJwt(builder.Configuration, identityServerAudience: "products_api");
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "DevRealmDrivers", options =>
    {
        options.Audience = "products-api";
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });
builder.Services.AddPermissionAndScopePolicies(
    permissions: ["products:read", "products:create", "products:update", "products:delete", "products:publish"],
    scopes: ["products.read", "products.write"]);

// ── Build ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Database migration + seeding at startup ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

    // Apply pending migrations (creates schema + HasData reference data)
    await db.Database.MigrateAsync();

    // Programmatic idempotent seeding for Contractor records (not in Production)
    if (!app.Environment.IsProduction())
        await DbInitializer.SeedAsync(db);
}

// ── Middleware pipeline ─────────────────────────────────────────────────────────
app.UseExceptionHandler();   // activates DomainExceptionHandler

// Lab 05: propagate X-Correlation-Id from Gateway to all log records in this service
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Contractor Registry API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Lab 06: gRPC endpoints ────────────────────────────────────────────────────
app.MapGrpcService<ContractorsGrpcService>();

// Lab 07: grpc.health.v1.Health plus /health/live and /health/ready (all environments).
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
