namespace Catalog.Domain.Models;

/// <summary>
/// Explicit M:N junction entity between Contractor and WorkCategory.
/// Has a composite PK (ContractorId + CategoryId) and an extra domain-significant field
/// QualificationDate — the date the contractor obtained qualification in that work category.
///
/// EF Core's automatic M:N cannot preserve this extra field, so an explicit entity is required
/// (same design rationale as ProductCategory.AddedAt in the course reference).
/// </summary>
public class ContractorWorkCategory
{
    public int ContractorId { get; set; }
    public int CategoryId { get; set; }

    /// <summary>
    /// Date when the contractor qualified (or was verified) for this work category.
    /// Defaults to SYSUTCDATETIME() at the DB level via Fluent API; set explicitly in programmatic seeding.
    /// </summary>
    public DateTime QualificationDate { get; set; }

    // Navigation
    public Contractor Contractor { get; set; } = null!;
    public WorkCategory WorkCategory { get; set; } = null!;
}
