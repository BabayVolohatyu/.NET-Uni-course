using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>
/// Fluent API configuration for ContractorWorkCategory — the explicit M:N junction entity.
/// Composite PK (ContractorId + CategoryId) and an extra domain field QualificationDate
/// make EF Core's automatic M:N unsuitable; an explicit entity is required.
///
/// This mirrors ProductCategoryConfiguration from the course reference
/// (ProductCategory.AddedAt → ContractorWorkCategory.QualificationDate).
/// </summary>
public sealed class ContractorWorkCategoryConfiguration : IEntityTypeConfiguration<ContractorWorkCategory>
{
    public void Configure(EntityTypeBuilder<ContractorWorkCategory> builder)
    {
        builder.ToTable("ContractorWorkCategories");

        // Composite primary key — same pattern as ProductCategory
        builder.HasKey(cwc => new { cwc.ContractorId, cwc.CategoryId });

        // Extra field — defaults to current UTC timestamp at the DB level
        builder.Property(cwc => cwc.QualificationDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // FK → Contractor (many side)
        builder.HasOne(cwc => cwc.Contractor)
            .WithMany(c => c.ContractorWorkCategories)
            .HasForeignKey(cwc => cwc.ContractorId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK → WorkCategory (many side)
        builder.HasOne(cwc => cwc.WorkCategory)
            .WithMany(wc => wc.ContractorWorkCategories)
            .HasForeignKey(cwc => cwc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);  // prevent category deletion while linked

        // Supporting index for querying recent qualifications by category
        builder.HasIndex(cwc => new { cwc.CategoryId, cwc.QualificationDate });
    }
}
