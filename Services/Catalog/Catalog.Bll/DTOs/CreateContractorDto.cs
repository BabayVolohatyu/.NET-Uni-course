using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTOs;

/// <summary>
/// Request DTO for POST /api/contractors.
/// DataAnnotations enforce basic structural constraints at the API boundary.
/// Complex business rules (EDRPOU uniqueness) are enforced in ContractorService.
/// FluentValidation provides richer rule descriptions in CreateContractorDtoValidator.
/// </summary>
public sealed class CreateContractorDto
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Name { get; set; } = "";

    /// <summary>8-digit Ukrainian business code, digits only.</summary>
    [Required]
    [StringLength(8, MinimumLength = 8)]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "EDRPOU must be exactly 8 numeric digits.")]
    public string EDRPOU { get; set; } = "";

    [Range(0.01, double.MaxValue, ErrorMessage = "Authorized capital must be positive.")]
    public decimal AuthorizedCapital { get; set; }

    public DateTime RegistrationDate { get; set; }

    // ── Optional nested detail ───────────────────────────────────────────────────
    [StringLength(1000, MinimumLength = 5)]
    public string? LegalAddress { get; set; }

    [StringLength(300, MinimumLength = 2)]
    public string? DirectorName { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? ContactEmail { get; set; }

    [StringLength(50)]
    public string? ContactPhone { get; set; }
}
