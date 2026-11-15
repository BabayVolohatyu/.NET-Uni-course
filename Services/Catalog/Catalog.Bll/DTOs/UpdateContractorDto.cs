using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTOs;

/// <summary>
/// Request DTO for PUT /api/contractors/{id}.
/// Only mutable fields are exposed; EDRPOU is immutable after registration.
/// </summary>
public sealed class UpdateContractorDto
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Range(0.01, double.MaxValue, ErrorMessage = "Authorized capital must be positive.")]
    public decimal AuthorizedCapital { get; set; }
}
