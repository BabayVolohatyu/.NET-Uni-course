using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class ProjectClassificationTagConfiguration : IEntityTypeConfiguration<ProjectClassificationTag>
{
    public void Configure(EntityTypeBuilder<ProjectClassificationTag> builder)
    {
        builder.ToTable("project_classification_tags");

        builder.HasKey(e => new { e.ProjectId, e.TagId });
        builder.Property(e => e.ProjectId).HasColumnName("project_id");
        builder.Property(e => e.TagId).HasColumnName("tag_id");
        builder.Property(e => e.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.TagId)
            .HasDatabaseName("ix_pct_tag_id");

        builder.HasOne(e => e.Project)
            .WithMany(p => p.ProjectClassificationTags)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pct_project");

        builder.HasOne(e => e.Tag)
            .WithMany(t => t.ProjectClassificationTags)
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pct_tag");
    }
}
