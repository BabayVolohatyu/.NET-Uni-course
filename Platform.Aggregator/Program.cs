using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Clients;
using System.Text.Json.Serialization;

// ── Platform.Aggregator — Aggregator Pattern implementation ───────────────────
// Receives composite requests → queries multiple microservices in parallel (Task.WhenAll)
// → assembles a single response with partial-failure tolerance (Warnings list).
//
// Registered in AppHost as "aggregator"; accessible to Gateway as http://aggregator.

var builder = WebApplication.CreateBuilder(args);

// Shared platform defaults: Serilog, OpenTelemetry, CorrelationId, health checks,
// service discovery, resilient HttpClient (all from ServiceDefaults)
builder.AddServiceDefaults();

// ── Typed HTTP clients ────────────────────────────────────────────────────────
// BaseAddress uses "https+http://<service-name>" — Aspire service discovery resolves
// the logical name to the real address (assigned dynamically by AppHost).
// CorrelationIdHandler is auto-added to all clients via ServiceDefaults.ConfigureHttpClientDefaults.
builder.Services.AddHttpClient<TendersClient>(
    c => c.BaseAddress = new Uri("https+http://tenders-api"));

builder.Services.AddHttpClient<ContractorsClient>(
    c => c.BaseAddress = new Uri("https+http://catalog-api"));

builder.Services.AddHttpClient<RiskAssessmentsClient>(
    c => c.BaseAddress = new Uri("https+http://risk-assessments-api"));

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
                      "Implements the Aggregator Pattern with partial failure tolerance."
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
