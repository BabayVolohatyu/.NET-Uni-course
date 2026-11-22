namespace Tenders.Domain.Models;

/// <summary>
/// Extended description of a tender; stored in a separate table (1:1 with Tenders).
/// Avoids widening the main Tenders row with large text columns.
/// </summary>
public class TenderDetails
{
    public long TenderId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>CPV / DK021 procurement category code.</summary>
    public string? CategoryCode { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public string? TechnicalSpecUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
