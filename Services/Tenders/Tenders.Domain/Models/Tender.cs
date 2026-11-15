namespace Tenders.Domain.Models;

/// <summary>
/// A public call for bids for infrastructure reconstruction work.
/// Corresponds to dbo.Tenders table. Has a status machine enforced by stored procedures.
/// </summary>
public class Tender
{
    public long Id { get; set; }
    public long ProcuringEntityId { get; set; }

    /// <summary>Short title of the procurement (e.g., "Repair of Kharkiv Road Section 14").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Planned maximum budget in UAH.</summary>
    public decimal ExpectedBudget { get; set; }

    /// <summary>Current lifecycle status (Draft → Published → BiddingOpen → EvaluationInProgress → Awarded / Cancelled).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? PublishedAt { get; set; }
    public DateTime? BiddingDeadline { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation — populated by GetWithDetailsAsync
    public TenderDetails? Details { get; set; }
    public List<Bid> Bids { get; set; } = new();
}
