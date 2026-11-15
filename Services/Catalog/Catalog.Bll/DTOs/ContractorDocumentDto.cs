namespace Catalog.Bll.DTOs;

/// <summary>Response DTO for a ContractorDocument (item in 1:N list).</summary>
public record ContractorDocumentDto(
    int Id,
    string DocumentType,
    string DocumentNumber,
    DateTime IssuedAt,
    DateTime? ExpiresAt);
