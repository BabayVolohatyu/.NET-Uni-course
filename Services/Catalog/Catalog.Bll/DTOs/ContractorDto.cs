using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTOs;

/// <summary>
/// Summary response DTO for a Contractor — used in paged list responses.
/// DataAnnotations here document the shape but are not validated (read-only DTO).
/// </summary>
public record ContractorDto(
    int Id,
    string Name,
    string EDRPOU,
    decimal AuthorizedCapital,
    DateTime RegistrationDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Full response DTO including 1:1 Detail, 1:N Documents, and M:N WorkCategories.
/// Returned by GET /api/contractors/{id}.
/// </summary>
public record ContractorWithDetailsDto(
    int Id,
    string Name,
    string EDRPOU,
    decimal AuthorizedCapital,
    DateTime RegistrationDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    ContractorDetailDto? Detail,
    IReadOnlyList<ContractorDocumentDto> Documents,
    IReadOnlyList<WorkCategoryDto> WorkCategories);
