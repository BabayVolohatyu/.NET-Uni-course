using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Catalog.Dal;

/// <summary>
/// Design-time factory used by the EF Core tooling (dotnet-ef) to create a CatalogDbContext
/// when running migrations from the command line:
///   dotnet ef migrations add InitialCreate --project Catalog.Dal --startup-project Catalog.Api
///
/// Falls back to a local appsettings.json if no environment-specific file exists.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        // Walk up from Catalog.Dal to Catalog.Api to find appsettings.json
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Catalog.Api");
        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ContractorRegistry")
            ?? "Server=localhost,1433;Database=ContractorRegistry;User Id=SA;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
