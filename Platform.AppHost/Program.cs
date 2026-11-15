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

// ── Microservices (Labs 02–04) ────────────────────────────────────────────────
// WithReference injects the connection string; WaitFor ensures the DB is ready first.
var tenders = builder.AddProject<Projects.Tenders_Api>("tenders-api")
    .WithReference(procurementDb)
    .WaitFor(procurementDb);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(contractorDb)
    .WaitFor(contractorDb);

var riskAssessments = builder.AddProject<Projects.RiskAssessments_WebApi>("risk-assessments-api")
    .WithReference(riskAssessmentDb)
    .WaitFor(riskAssessmentDb);

// ── Aggregator ────────────────────────────────────────────────────────────────
// Receives references to all three microservices (for service discovery).
// WaitFor ensures services are ready before Aggregator starts calling them.
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(tenders)
    .WithReference(catalog)
    .WithReference(riskAssessments)
    .WaitFor(tenders)
    .WaitFor(catalog)
    .WaitFor(riskAssessments);

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
