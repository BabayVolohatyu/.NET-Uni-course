using Microsoft.Extensions.Hosting;
using RiskAssessments.Application;
using RiskAssessments.Application.Mapping;
using RiskAssessments.Infrastructure;
using RiskAssessments.Infrastructure.Indexing;
using RiskAssessments.Infrastructure.Persistence;
using RiskAssessments.Infrastructure.Seeding;
using RiskAssessments.WebApi.ExceptionHandling;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Lab 05: Platform ServiceDefaults ──────────────────────────────────────────
// Configures Serilog (structured JSON logging with enrichers), OpenTelemetry
// (tracing + metrics for ASP.NET Core, HttpClient, MongoDB via DiagnosticSources),
// CorrelationId, health checks, service discovery, and resilient HttpClient defaults.
builder.AddServiceDefaults();

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

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

builder.Services.AddHealthChecks()
    .AddMongoDb(
        clientFactory: sp => sp.GetRequiredService<MongoDbContext>().Client,
        databaseNameFactory: sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbOptions>>().Value.DatabaseName,
        name: "mongodb",
        timeout: TimeSpan.FromSeconds(3));

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

// ── Lab 05: Health check endpoints (/health and /alive) ───────────────────────
// MapDefaultEndpoints provides /health (full readiness) and /alive (liveness).
// The existing .AddHealthChecks().AddMongoDb(...) check is included automatically.
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
