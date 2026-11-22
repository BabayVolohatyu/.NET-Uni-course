namespace Catalog.Domain.Models;

/// <summary>
/// A qualification document or license attached to a Contractor — 1:N relationship.
/// Examples: construction licenses, ISO certificates, tax clearance certificates.
/// </summary>
public class ContractorDocument
{
    public int Id { get; set; }
    public int ContractorId { get; set; }

    /// <summary>Type of document, e.g. "LicenseCC1", "ISO9001", "TaxClearance".</summary>
    public string DocumentType { get; set; } = "";

    /// <summary>Official document number as printed on the certificate.</summary>
    public string DocumentNumber { get; set; } = "";

    /// <summary>Date the document was issued by the authority.</summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>Expiry date — null means the document does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }

    // Navigation — back reference
    public Contractor Contractor { get; set; } = null!;
}
