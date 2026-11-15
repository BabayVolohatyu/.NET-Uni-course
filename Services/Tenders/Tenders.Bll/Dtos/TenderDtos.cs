using System.ComponentModel.DataAnnotations;

namespace Tenders.Bll.Dtos;

/// <summary>Read model for a tender (without details).</summary>
public record TenderDto(
    long Id,
    long ProcuringEntityId,
    string Title,
    decimal ExpectedBudget,
    string Status,
    DateTime? PublishedAt,
    DateTime? BiddingDeadline,
    DateTime CreatedAt);

/// <summary>Read model that includes extended TenderDetails.</summary>
public record TenderWithDetailsDto(
    long Id,
    long ProcuringEntityId,
    string Title,
    decimal ExpectedBudget,
    string Status,
    DateTime? PublishedAt,
    DateTime? BiddingDeadline,
    DateTime CreatedAt,
    TenderDetailsDto? Details);

/// <summary>Details portion of a tender.</summary>
public record TenderDetailsDto(
    long TenderId,
    string Description,
    string? CategoryCode,
    string DeliveryAddress,
    string? TechnicalSpecUrl);

/// <summary>Input model for creating a new tender. DataAnnotations guard the API layer.</summary>
public class CreateTenderDto
{
    [Range(1, long.MaxValue)]
    public long ProcuringEntityId { get; set; }

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "ExpectedBudget must be positive.")]
    public decimal ExpectedBudget { get; set; }

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? CategoryCode { get; set; }

    [Required, MaxLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [MaxLength(1000), Url]
    public string? TechnicalSpecUrl { get; set; }
}

/// <summary>Input model for changing a tender's status.</summary>
public class ChangeTenderStatusDto
{
    [Required]
    public string NewStatus { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>Read model for a single status history entry.</summary>
public record TenderStatusHistoryDto(
    long Id,
    long TenderId,
    string FromStatus,
    string ToStatus,
    DateTime ChangedAt,
    string? ChangedBy,
    string? Reason);
