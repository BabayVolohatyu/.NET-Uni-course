using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>
/// Fluent API configuration for WorkCategory (reference / lookup table).
/// Includes static HasData seeding for the five standard reconstruction work categories
/// with fixed Ids — suitable for migration-based seeding of reference data.
/// </summary>
public sealed class WorkCategoryConfiguration : IEntityTypeConfiguration<WorkCategory>
{
    public void Configure(EntityTypeBuilder<WorkCategory> builder)
    {
        builder.ToTable("WorkCategories");
        builder.HasKey(wc => wc.Id);

        builder.Property(wc => wc.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(wc => wc.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(wc => wc.Description)
            .HasMaxLength(1000);

        builder.HasIndex(wc => wc.Code).IsUnique();
        builder.HasIndex(wc => wc.Name);

        // ── Static (model) seed — reference data with stable Ids ──────────────
        // HasData requires all required fields; constructor used to satisfy EF's
        // shadow-property tracking for private setters.
        builder.HasData(
            new WorkCategory(1, "ROAD", "Road and Bridge Repair",
                "Construction and repair of roads, bridges, and transport infrastructure."),
            new WorkCategory(2, "BLDG", "Building and Construction",
                "Residential and commercial building construction, schools, hospitals."),
            new WorkCategory(3, "ELEC", "Electrical Works",
                "Power grids, substations, street lighting, and industrial wiring."),
            new WorkCategory(4, "PIPE", "Pipeline and Water Supply",
                "Water supply networks, sewage systems, and water treatment facilities."),
            new WorkCategory(5, "HEAT", "Heating Systems",
                "District heating networks, boiler houses, and thermal insulation.")
        );
    }
}
