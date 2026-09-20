using SchemaHunter.Procurement.Infrastructure;
using SchemaHunter.Procurement.Infrastructure.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register connection factory as singleton (cheap, stateless)
var connectionString = builder.Configuration.GetConnectionString("ProcurementDb")
    ?? Environment.GetEnvironmentVariable("PROCUREMENT_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "SQL Server connection string not configured. " +
        "Set ConnectionStrings:ProcurementDb in appsettings.json or " +
        "the PROCUREMENT_CONNECTION_STRING environment variable.");

builder.Services.AddSingleton(new ProcurementConnectionFactory(connectionString));
builder.Services.AddScoped<TenderRepository>();
builder.Services.AddScoped<SupplierRepository>();

// ─── Pipeline ─────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
