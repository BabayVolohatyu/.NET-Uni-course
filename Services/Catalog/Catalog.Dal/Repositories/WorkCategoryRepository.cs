using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Specific repository for WorkCategory — demonstrates <b>Explicit Loading</b>.
///
/// GetWithContractorsAsync: performs TWO separate SQL queries:
///   1. SELECT … FROM WorkCategories WHERE Id = @id   (main entity)
///   2. SELECT … FROM ContractorWorkCategories WHERE CategoryId = @id  (related data, only if needed)
///
/// Compare with ContractorRepository.GetWithDetailsAsync (Eager Loading — one query with JOINs).
/// Use Explicit Loading when related data is large and needed only conditionally.
/// </summary>
public sealed class WorkCategoryRepository : GenericRepository<WorkCategory>, IWorkCategoryRepository
{
    public WorkCategoryRepository(CatalogDbContext context) : base(context) { }

    // ── Explicit Loading ─────────────────────────────────────────────────────────
    /// <summary>
    /// Two-query pattern:
    ///   Query 1 — fetch WorkCategory by Id.
    ///   Query 2 — if the category exists, load its ContractorWorkCategories with Contractor included.
    ///
    /// The second query fires only when the entity was found (conditional lazy hydration).
    /// </summary>
    public async Task<WorkCategory?> GetWithContractorsAsync(int id, CancellationToken ct = default)
    {
        // Query 1 — base entity, tracked so Entry() can reach its navigation
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return null;

        // Query 2 — explicit: load related ContractorWorkCategories + Contractor
        await Context.Entry(category)
            .Collection(c => c.ContractorWorkCategories)
            .Query()
            .Include(cwc => cwc.Contractor)
            .LoadAsync(ct);

        return category;
    }

    public Task<WorkCategory?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(wc => wc.Code == code, ct);
}
