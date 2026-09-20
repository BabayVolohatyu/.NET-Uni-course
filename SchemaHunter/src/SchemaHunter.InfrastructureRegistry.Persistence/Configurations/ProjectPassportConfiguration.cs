using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class ProjectPassportConfiguration : IEntityTypeConfiguration<ProjectPassport>
{
    public void Configure(EntityTypeBuilder<ProjectPassport> builder)
    {
        builder.ToTable("project_passports");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(e => e.EdessrNumber).HasColumnName("edessr_number").HasMaxLength(100);
        builder.Property(e => e.TechnicalPermitNumber).HasColumnName("technical_permit_number").HasMaxLength(100);
        builder.Property(e => e.GpsLatitude).HasColumnName("gps_latitude").HasPrecision(9, 6);
        builder.Property(e => e.GpsLongitude).HasColumnName("gps_longitude").HasPrecision(9, 6);
        builder.Property(e => e.BuiltAreaSqm).HasColumnName("built_area_sqm").HasPrecision(10, 2);
        builder.Property(e => e.ConstructionStartDate).HasColumnName("construction_start_date");
        builder.Property(e => e.EstimatedCompletionDate).HasColumnName("estimated_completion_date");
        builder.Property(e => e.ActualCompletionDate).HasColumnName("actual_completion_date");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

        // Unique FK enforces 1:1 relationship at the database level
        builder.HasIndex(e => e.ProjectId)
            .IsUnique()
            .HasDatabaseName("ix_project_passports_project_id_unique");

        // Filtered unique index — EdessrNumber is only unique among assigned passports
        builder.HasIndex(e => e.EdessrNumber)
            .IsUnique()
            .HasFilter("[edessr_number] IS NOT NULL")
            .HasDatabaseName("ix_project_passports_edessr_number_unique");

        builder.HasOne(e => e.Project)
            .WithOne(p => p.Passport)
            .HasForeignKey<ProjectPassport>(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_project_passports_project");
    }
}
