using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

/// <summary>
/// EF Core DbContext for the Contractor Registry bounded context (Project №2).
/// All entity configurations are discovered automatically via ApplyConfigurationsFromAssembly,
/// so OnModelCreating stays clean — one line, no per-entity clutter.
/// </summary>
public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<ContractorDetail> ContractorDetails => Set<ContractorDetail>();
    public DbSet<ContractorDocument> ContractorDocuments => Set<ContractorDocument>();
    public DbSet<WorkCategory> WorkCategories => Set<WorkCategory>();
    public DbSet<ContractorWorkCategory> ContractorWorkCategories => Set<ContractorWorkCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
}
