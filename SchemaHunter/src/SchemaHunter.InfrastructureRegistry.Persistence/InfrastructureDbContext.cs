using Microsoft.EntityFrameworkCore;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence;

public class InfrastructureDbContext : DbContext
{
    public InfrastructureDbContext(DbContextOptions<InfrastructureDbContext> options)
        : base(options)
    {
    }

    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<ReconstructionProject> ReconstructionProjects => Set<ReconstructionProject>();
    public DbSet<ProjectPassport> ProjectPassports => Set<ProjectPassport>();
    public DbSet<ClassificationTag> ClassificationTags => Set<ClassificationTag>();
    public DbSet<ProjectClassificationTag> ProjectClassificationTags => Set<ProjectClassificationTag>();
    public DbSet<FundingSource> FundingSources => Set<FundingSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyInfrastructureConfigurations();
    }
}
