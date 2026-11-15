// Platform.AppHost — .NET Aspire orchestrator for the Procurement Risk Assessment platform.
// Starts all microservices, databases, Gateway, and Aggregator with one "dotnet run".
// ⚠️ This project contains NO business logic — it is a development-time orchestrator only.
//    In production, Kubernetes (Lab 10) takes over lifecycle management.

var builder = DistributedApplication.CreateBuilder(args);

// ── SQL Server container ──────────────────────────────────────────────────────
// Password comes from user-secrets (AppHost) or the Dashboard prompt on first run.
// Never hard-code passwords in source control.
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("procurement-sql-data");   // persistent volume: data survives restarts

// Databases match the connection string names the services already read from configuration.
// Aspire injects them as: ConnectionStrings:<DbName> environment variable.
var procurementDb = sql.AddDatabase("ProcurementBidding");   // Lab 02 — Tenders.Api (ADO.NET + Dapper)
var contractorDb  = sql.AddDatabase("ContractorRegistry");   // Lab 03 — Catalog.Api (EF Core)

// ── MongoDB container ─────────────────────────────────────────────────────────
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("procurement-mongo-data"); // persistent volume

var riskAssessmentDb = mongo.AddDatabase("RiskAssessmentDb"); // Lab 04 — RiskAssessments.WebApi (MongoDB)

// ── Redis container (Lab 06) ──────────────────────────────────────────────────
// Provides distributed caching (L2) shared across all service replicas.
// Aspire automatically injects ConnectionStrings:redis into every service that has WithReference(redis).
// WithDataVolume ensures cached entries survive container restarts (persist to Docker volume).
var redis = builder.AddRedis("redis")
    .WithDataVolume("procurement-redis-data");

// ── Microservices (Labs 02–04, extended in Lab 06 with Redis) ─────────────────
// WithReference injects the connection string; WaitFor ensures the DB is ready first.
// Lab 06: each service also receives a Redis reference for distributed caching.
var tenders = builder.AddProject<Projects.Tenders_Api>("tenders-api")
    .WithReference(procurementDb).WaitFor(procurementDb)
    .WithReference(redis).WaitFor(redis);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(contractorDb).WaitFor(contractorDb)
    .WithReference(redis).WaitFor(redis);

var riskAssessments = builder.AddProject<Projects.RiskAssessments_WebApi>("risk-assessments-api")
    .WithReference(riskAssessmentDb).WaitFor(riskAssessmentDb)
    .WithReference(redis).WaitFor(redis);

// ── Aggregator (Lab 06: Redis for caching aggregated responses) ───────────────
// Receives references to all three microservices (for service discovery).
// WaitFor ensures services are ready before Aggregator starts calling them.
// gRPC clients replace HTTP clients for microservice-to-microservice communication.
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(tenders).WaitFor(tenders)
    .WithReference(catalog).WaitFor(catalog)
    .WithReference(riskAssessments).WaitFor(riskAssessments)
    .WithReference(redis).WaitFor(redis);

// ── API Gateway (YARP) ────────────────────────────────────────────────────────
// Fixed port 5000 for convenient development — clients always connect to localhost:5000.
// Gateway must start last: it depends on Aggregator being ready too.
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(tenders)
    .WithReference(catalog)
    .WithReference(riskAssessments)
    .WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();
