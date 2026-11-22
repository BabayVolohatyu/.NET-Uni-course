using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Specific repository for Contractor — demonstrates <b>Eager Loading</b>.
///
/// GetWithDetailsAsync: loads the full contractor card (Detail, Documents, WorkCategories)
/// in a SINGLE SQL query using Include / ThenInclude (one round-trip, one JOIN).
///
/// Contrast with WorkCategoryRepository (Explicit Loading, two separate queries).
/// </summary>
public sealed class ContractorRepository : GenericRepository<Contractor>, IContractorRepository
{
    public ContractorRepository(CatalogDbContext context) : base(context) { }

    // ── Eager Loading ────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns the contractor card with ALL related data loaded in one SQL query.
    /// EF Core translates Include / ThenInclude chains into LEFT JOINs.
    /// </summary>
    public Task<Contractor?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => Set
            .Include(c => c.Detail)                                    // 1:1
            .Include(c => c.Documents)                                 // 1:N
            .Include(c => c.ContractorWorkCategories)                  // M:N junction
                .ThenInclude(cwc => cwc.WorkCategory)                  // → WorkCategory
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    // ── Uniqueness check ─────────────────────────────────────────────────────────
    public Task<bool> ExistsByEdrpouAsync(string edrpou, CancellationToken ct = default)
        => Set.AnyAsync(c => c.EDRPOU == edrpou, ct);

    // ── Specification support ────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Contractor>> ListAsync(
        ISpecification<Contractor> spec, CancellationToken ct = default)
        => await Set.WithSpecification(spec).ToListAsync(ct);

    public Task<int> CountAsync(ISpecification<Contractor> spec, CancellationToken ct = default)
        => Set.WithSpecification(spec).CountAsync(ct);
}
