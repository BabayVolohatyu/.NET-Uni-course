using Catalog.Domain.Models;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Specific repository for the ContractorWorkCategory M:N junction.
/// Demonstrates LINQ-to-Entities queries through the intermediate entity
/// using its own domain-significant field (QualificationDate).
/// </summary>
public interface IContractorWorkCategoryRepository : IGenericRepository<ContractorWorkCategory>
{
    /// <summary>
    /// LINQ M:N query: returns contractors that obtained qualification in a given work category
    /// on or after the specified date. Navigates through the junction entity and uses
    /// its QualificationDate field — which automatic M:N could not expose.
    /// </summary>
    Task<IReadOnlyList<Contractor>> GetRecentlyQualifiedAsync(
        string categoryCode, DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Returns all ContractorWorkCategory links for a specific contractor,
    /// including the WorkCategory navigation for display purposes.
    /// </summary>
    Task<IReadOnlyList<ContractorWorkCategory>> GetByContractorAsync(
        int contractorId, CancellationToken ct = default);
}
