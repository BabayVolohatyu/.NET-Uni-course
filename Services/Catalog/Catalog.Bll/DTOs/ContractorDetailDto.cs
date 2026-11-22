namespace Catalog.Bll.DTOs;

/// <summary>Response DTO for ContractorDetail (1:1 nested in ContractorWithDetailsDto).</summary>
public record ContractorDetailDto(
    string LegalAddress,
    string DirectorName,
    string? ContactEmail,
    string? ContactPhone);
