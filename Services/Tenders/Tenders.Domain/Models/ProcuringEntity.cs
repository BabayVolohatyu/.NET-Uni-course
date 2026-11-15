namespace Tenders.Domain.Models;

/// <summary>
/// A state agency, city council, or municipality that publishes reconstruction tenders.
/// Corresponds to dbo.ProcuringEntities table in ProcurementBidding DB.
/// </summary>
public class ProcuringEntity
{
    public long Id { get; set; }

    /// <summary>Full legal name of the procuring organisation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ukrainian business-registry code (always 8 digits).</summary>
    public string EDRPOU { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}
