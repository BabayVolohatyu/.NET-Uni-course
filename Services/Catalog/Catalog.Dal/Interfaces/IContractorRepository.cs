using Ardalis.Specification;
using Catalog.Domain.Models;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Specific repository for Contractor with Eager Loading queries and specification support.
/// Extends IGenericRepository with domain-specific queries.
/// </summary>
public interface IContractorRepository : IGenericRepository<Contractor>
{
    /// <summary>
    /// Eager Loading: loads a contractor with ALL related data (Detail, Documents, WorkCategories)
    /// in a SINGLE SQL query using Include / ThenInclude.
    /// </summary>
    Task<Contractor?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Checks EDRPOU uniqueness before INSERT — service-layer guard before DB constraint fires.
    /// </summary>
    Task<bool> ExistsByEdrpouAsync(string edrpou, CancellationToken ct = default);

    /// <summary>
    /// Returns a paged list of contractors matching the given Ardalis Specification.
    /// </summary>
    Task<IReadOnlyList<Contractor>> ListAsync(ISpecification<Contractor> spec, CancellationToken ct = default);

    /// <summary>
    /// Counts contractors matching the given Ardalis Specification (for totalCount in PagedResult).
    /// </summary>
    Task<int> CountAsync(ISpecification<Contractor> spec, CancellationToken ct = default);
}
