using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> builder)
    {
        builder.ToTable("asset_categories");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("ix_asset_categories_code_unique");

        builder.HasQueryFilter(e => !e.IsDeleted);

        // ── Reference seed data ───────────────────────────────────────────────
        builder.HasData(
            new AssetCategory { Id = 1, Code = "ROAD",   Name = "Road Infrastructure",     Description = "Highways, regional roads, urban streets.", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AssetCategory { Id = 2, Code = "BLDG",   Name = "Building",                Description = "Residential, educational, medical, administrative buildings.", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AssetCategory { Id = 3, Code = "BRDG",   Name = "Bridge",                  Description = "Road and railway bridges, pedestrian crossings.", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AssetCategory { Id = 4, Code = "ENERGY", Name = "Energy Infrastructure",   Description = "Power stations, substations, transmission lines.", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AssetCategory { Id = 5, Code = "WATER",  Name = "Water Supply",            Description = "Water treatment plants, pipelines, pump stations.", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
