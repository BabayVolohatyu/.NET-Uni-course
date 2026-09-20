using MongoDB.Driver;
using Scalar.AspNetCore;
using SchemaHunter.CommunityAudits.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "MongoDB connection string not configured. " +
        "Set ConnectionStrings:MongoDb in appsettings.json or " +
        "the MONGO_CONNECTION_STRING environment variable.");

var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? Environment.GetEnvironmentVariable("MONGO_AUDITS_DB")
    ?? "SchemaHunterAudits";

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));

builder.Services.AddScoped<InspectionAuditRepository>();
builder.Services.AddScoped<DiscussionThreadRepository>();

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
