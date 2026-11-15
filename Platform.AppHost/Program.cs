// Platform.AppHost — .NET Aspire orchestration
// Starts SQL Server in a container + the Tenders API in one command.
// Run: dotnet run --project Platform.AppHost
// Then open the Aspire Dashboard URL shown in the console.

var builder = DistributedApplication.CreateBuilder(args);

// ── Option A (default): use an existing ProcurementBidding DB
// The connection string name ("ProcurementBidding") matches the key in Tenders.Api/appsettings.json.
// Aspire injects it automatically via WithReference — no code changes needed in Tenders.Api.
//
// Provide the value in Platform.AppHost/appsettings.json (not committed — add to .gitignore if needed):
//   { "ConnectionStrings": { "ProcurementBidding": "Server=localhost,1433;Database=..." } }
var procurementDb = builder.AddConnectionString("ProcurementBidding");

// ── Option B: SQL Server container (uncomment to spin up a fresh container)
// var sql = builder.AddSqlServer("sql", password: builder.CreateStablePassword("sql"))
//                  .WithDataVolume("procurement-data");
// var procurementDb = sql.AddDatabase("ProcurementBidding");
// After first run apply schema: docker exec -it <container> sqlcmd ...

// ── Tenders API ────────────────────────────────────────────────────────────────
builder.AddProject<Projects.Tenders_Api>("tenders-api")
       .WithReference(procurementDb)
       .WithExternalHttpEndpoints();

builder.Build().Run();
