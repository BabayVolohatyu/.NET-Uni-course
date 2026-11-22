namespace Tenders.Domain.Models;

/// <summary>
/// A contractor's price proposal for a specific tender (M:N bridge table).
/// ContractorName and ContractorEDRPOU are SNAPSHOTS fixed at submission time — never updated.
/// This follows the same duplication policy as OrderItem.ProductName in Microsoft eShop.
/// </summary>
public class Bid
{
    public long TenderId { get; set; }
    public long ContractorId { get; set; }

    /// <summary>Snapshot of contractor name at bid submission time.</summary>
    public string ContractorName { get; set; } = string.Empty;

    /// <summary>Snapshot of contractor EDRPOU at bid submission time.</summary>
    public string ContractorEDRPOU { get; set; } = string.Empty;

    public decimal ProposedPrice { get; set; }
    public decimal PriceReductionPct { get; set; }

    public DateTime SubmittedAt { get; set; }
    public bool IsWinner { get; set; }
    public bool IsDisqualified { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}
