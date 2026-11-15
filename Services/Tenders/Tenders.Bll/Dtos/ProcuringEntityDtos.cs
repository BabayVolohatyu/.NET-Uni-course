using System.ComponentModel.DataAnnotations;

namespace Tenders.Bll.Dtos;

/// <summary>Read model returned by GET endpoints for a procuring entity.</summary>
public record ProcuringEntityDto(
    long Id,
    string Name,
    string EDRPOU,
    string Region,
    string? ContactEmail,
    DateTime CreatedAt);

/// <summary>Input model for creating a new procuring entity. Validated with DataAnnotations before hitting BLL.</summary>
public class CreateProcuringEntityDto
{
    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(8, MinimumLength = 8, ErrorMessage = "EDRPOU must be exactly 8 digits.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "EDRPOU must consist of exactly 8 digits.")]
    public string EDRPOU { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Region { get; set; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? ContactEmail { get; set; }
}
