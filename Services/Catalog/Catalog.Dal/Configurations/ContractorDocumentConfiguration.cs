using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>
/// Fluent API configuration for ContractorDocument — the 1:N child entity.
/// Each contractor can hold multiple qualification documents and licenses.
/// </summary>
public sealed class ContractorDocumentConfiguration : IEntityTypeConfiguration<ContractorDocument>
{
    public void Configure(EntityTypeBuilder<ContractorDocument> builder)
    {
        builder.ToTable("ContractorDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.DocumentNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.IssuedAt)
            .HasColumnType("date");

        builder.Property(d => d.ExpiresAt)
            .HasColumnType("date");

        // Partial index: quickly find all active (non-expired) documents for a contractor
        builder.HasIndex(d => new { d.ContractorId, d.ExpiresAt });
        builder.HasIndex(d => d.DocumentNumber);
    }
}
