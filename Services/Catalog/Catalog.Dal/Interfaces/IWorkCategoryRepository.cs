using Catalog.Domain.Models;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Specific repository for WorkCategory with Explicit Loading query.
/// Extends IGenericRepository with domain-specific queries.
/// </summary>
public interface IWorkCategoryRepository : IGenericRepository<WorkCategory>
{
    /// <summary>
    /// Explicit Loading: loads a WorkCategory first, then — in a SEPARATE query —
    /// eagerly loads its ContractorWorkCategories with Contractor navigation included.
    /// Demonstrates two-query Explicit Loading pattern (vs. single-query Eager Loading).
    /// </summary>
    Task<WorkCategory?> GetWithContractorsAsync(int id, CancellationToken ct = default);

    /// <summary>Looks up a WorkCategory by its unique code (e.g. "ROAD").</summary>
    Task<WorkCategory?> GetByCodeAsync(string code, CancellationToken ct = default);
}
