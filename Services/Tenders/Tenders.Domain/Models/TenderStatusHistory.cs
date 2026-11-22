namespace Tenders.Domain.Models;

/// <summary>
/// Immutable audit log entry for a single status transition of a tender.
/// Needed for compliance and anomaly-detection history (e.g., detect suspiciously fast transitions).
/// </summary>
public class TenderStatusHistory
{
    public long Id { get; set; }
    public long TenderId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
}
