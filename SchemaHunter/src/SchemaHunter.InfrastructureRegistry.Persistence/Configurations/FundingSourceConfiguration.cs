using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence.Configurations;

public class FundingSourceConfiguration : IEntityTypeConfiguration<FundingSource>
{
    public void Configure(EntityTypeBuilder<FundingSource> builder)
    {
        builder.ToTable("funding_sources");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(e => e.FunderName).HasColumnName("funder_name").IsRequired().HasMaxLength(500);
        builder.Property(e => e.FundType).HasColumnName("fund_type").IsRequired().HasMaxLength(100);
        builder.Property(e => e.AllocatedAmount).HasColumnName("allocated_amount").HasPrecision(18, 2);
        builder.Property(e => e.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(3).HasDefaultValue("UAH");
        builder.Property(e => e.GrantAgreementNumber).HasColumnName("grant_agreement_number").HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.ProjectId)
            .HasDatabaseName("ix_funding_sources_project_id");

        builder.HasIndex(e => e.FundType)
            .HasDatabaseName("ix_funding_sources_fund_type");

        builder.HasOne(e => e.Project)
            .WithMany(p => p.FundingSources)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_funding_sources_project");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
