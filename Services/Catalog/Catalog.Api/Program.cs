using Catalog.Api.Middleware;
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
builder.Services.AddScoped<IContractorService, ContractorService>();
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
app.MapControllers();

// ── Lab 05: Health check endpoints (/health and /alive) ───────────────────────
app.MapDefaultEndpoints();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
