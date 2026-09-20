using Microsoft.EntityFrameworkCore;
using SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

namespace SchemaHunter.InfrastructureRegistry.Persistence;

public static class InfrastructureModelBuilderExtensions
{
    public static ModelBuilder ApplyInfrastructureConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AssetCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ReconstructionProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectPassportConfiguration());
        modelBuilder.ApplyConfiguration(new ClassificationTagConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectClassificationTagConfiguration());
        modelBuilder.ApplyConfiguration(new FundingSourceConfiguration());

        return modelBuilder;
    }
}
