namespace Catalog.Bll.DTOs;

/// <summary>Response DTO for WorkCategory reference data.</summary>
public record WorkCategoryDto(int Id, string Code, string Name, string? Description);
