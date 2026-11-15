using Catalog.Bll.DTOs;

namespace Catalog.Bll.Interfaces;

/// <summary>Business-logic contract for WorkCategory reference data.</summary>
public interface IWorkCategoryService
{
    Task<IReadOnlyList<WorkCategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<WorkCategoryDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WorkCategoryDto> GetByCodeAsync(string code, CancellationToken ct = default);
}
