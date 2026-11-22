using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>
/// Fluent API configuration for ContractorDetail — the 1:1 "details" entity.
/// ContractorId is simultaneously the PK and FK (shared primary key pattern),
/// identical to ProductDetail.ProductId in the course reference.
/// </summary>
public sealed class ContractorDetailConfiguration : IEntityTypeConfiguration<ContractorDetail>
{
    public void Configure(EntityTypeBuilder<ContractorDetail> builder)
    {
        builder.ToTable("ContractorDetails");

        // PK is the same column as the FK → shared primary key (1:1 owned side)
        builder.HasKey(d => d.ContractorId);

        builder.Property(d => d.LegalAddress)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.DirectorName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.ContactEmail)
            .HasMaxLength(320);     // RFC 5321 max email length

        builder.Property(d => d.ContactPhone)
            .HasMaxLength(50);
    }
}
