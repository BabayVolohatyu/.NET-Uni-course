using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class ClassificationTagConfiguration : IEntityTypeConfiguration<ClassificationTag>
{
    public void Configure(EntityTypeBuilder<ClassificationTag> builder)
    {
        builder.ToTable("classification_tags");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(50);
        builder.Property(e => e.Label).HasColumnName("label").IsRequired().HasMaxLength(200);
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("ix_classification_tags_code_unique");

        builder.HasQueryFilter(e => !e.IsDeleted);

        // ── Reference seed data ───────────────────────────────────────────────
        builder.HasData(
            new ClassificationTag { Id = 1, Code = "PRIORITY",          Label = "Priority Project",        IsDeleted = false },
            new ClassificationTag { Id = 2, Code = "DONOR_FUNDED",       Label = "Donor Funded",            IsDeleted = false },
            new ClassificationTag { Id = 3, Code = "UNESCO_LISTED",      Label = "UNESCO Listed Site",      IsDeleted = false },
            new ClassificationTag { Id = 4, Code = "CRITICAL_INFRA",     Label = "Critical Infrastructure", IsDeleted = false }
        );
    }
}
