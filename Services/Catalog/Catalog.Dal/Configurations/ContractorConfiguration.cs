using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>
/// Fluent API configuration for the Contractor aggregate root.
/// Separates infrastructure concerns (table name, column types, constraints, indexes)
/// from the domain entity so the entity remains a clean POCO.
/// </summary>
public sealed class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
{
    public void Configure(EntityTypeBuilder<Contractor> builder)
    {
        builder.ToTable("Contractors", t =>
            t.HasCheckConstraint("CK_Contractors_AuthorizedCapital", "[AuthorizedCapital] > 0"));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.EDRPOU)
            .IsRequired()
            .HasMaxLength(8)
            .IsFixedLength();   // CHAR(8) — always exactly 8 digits

        builder.Property(c => c.AuthorizedCapital)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.RegistrationDate)
            .HasColumnType("date");     // date only — no time component needed

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(c => c.UpdatedAt);

        // Unique constraint on EDRPOU — guards against duplicate registrations
        builder.HasIndex(c => c.EDRPOU).IsUnique();
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.IsActive);

        // 1:1 — Contractor has one ContractorDetail (optional; shared PK)
        builder.HasOne(c => c.Detail)
            .WithOne(d => d.Contractor)
            .HasForeignKey<ContractorDetail>(d => d.ContractorId);

        // 1:N — Contractor has many Documents; cascade delete removes docs when contractor is deleted
        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Contractor)
            .HasForeignKey(d => d.ContractorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
