namespace Catalog.Domain.Models;

/// <summary>
/// Extended details for a Contractor — 1:1 relationship where ContractorId is both PK and FK
/// (shared primary key pattern, same as ProductDetail in the course reference).
/// </summary>
public class ContractorDetail
{
    /// <summary>Shared PK = FK to Contractors.Id.</summary>
    public int ContractorId { get; set; }

    /// <summary>Official registered legal address.</summary>
    public string LegalAddress { get; set; } = "";

    /// <summary>Full name of the director / CEO as registered.</summary>
    public string DirectorName { get; set; } = "";

    /// <summary>Primary business contact email.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Primary business contact phone (optional).</summary>
    public string? ContactPhone { get; set; }

    // Navigation — back reference
    public Contractor Contractor { get; set; } = null!;
}
