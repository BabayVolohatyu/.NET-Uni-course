using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SchemaHunter.InfrastructureRegistry.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("InfrastructureDb")
    ?? Environment.GetEnvironmentVariable("REGISTRY_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "SQL Server connection string not configured. " +
        "Set ConnectionStrings:InfrastructureDb in appsettings.json or " +
        "the REGISTRY_CONNECTION_STRING environment variable.");

builder.Services.AddDbContext<InfrastructureDbContext>(options =>
    options.UseSqlServer(connectionString));

// ─── Pipeline ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Apply EF Core migrations & seed reference data on startup ───────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InfrastructureDbContext>();
    db.Database.Migrate();              // creates schema + HasData seed rows
    await DbSeeder.SeedAsync(db);       // inserts projects, passports, funding, tags
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
