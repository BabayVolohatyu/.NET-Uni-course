using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class ReconstructionProjectConfiguration : IEntityTypeConfiguration<ReconstructionProject>
{
    public void Configure(EntityTypeBuilder<ReconstructionProject> builder)
    {
        builder.ToTable("reconstruction_projects");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.AssetCode).HasColumnName("asset_code").IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(500);
        builder.Property(e => e.AssetCategoryId).HasColumnName("asset_category_id").IsRequired();
        builder.Property(e => e.AllocatedBudget).HasColumnName("allocated_budget").HasPrecision(18, 2);
        builder.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(50);
        builder.Property(e => e.Region).HasColumnName("region").IsRequired().HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(e => e.AssetCode)
            .IsUnique()
            .HasDatabaseName("ix_reconstruction_projects_asset_code_unique");

        builder.HasIndex(e => e.AssetCategoryId)
            .HasDatabaseName("ix_reconstruction_projects_category_id");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_reconstruction_projects_status");

        builder.HasOne(e => e.AssetCategory)
            .WithMany(c => c.Projects)
            .HasForeignKey(e => e.AssetCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_reconstruction_projects_asset_category");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
