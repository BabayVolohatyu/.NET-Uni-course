using Catalog.Dal.Interfaces;
using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Specific repository for the ContractorWorkCategory M:N junction — demonstrates
/// <b>LINQ-to-Entities query through the intermediate entity</b>.
///
/// GetRecentlyQualifiedAsync: navigates Contractor ← ContractorWorkCategory → WorkCategory
/// and filters on the junction entity's own field (QualificationDate) — this is only
/// possible because we modelled an explicit intermediate entity instead of relying on
/// EF Core's automatic M:N (which hides the junction table from LINQ).
/// </summary>
public sealed class ContractorWorkCategoryRepository
    : GenericRepository<ContractorWorkCategory>, IContractorWorkCategoryRepository
{
    public ContractorWorkCategoryRepository(CatalogDbContext context) : base(context) { }

    // ── LINQ query through the M:N junction ──────────────────────────────────────
    /// <summary>
    /// Returns contractors that obtained qualification in the given work-category code
    /// on or after <paramref name="since"/>.
    ///
    /// The LINQ Where clause references ContractorWorkCategory.WorkCategory.Code and
    /// ContractorWorkCategory.QualificationDate — both are on the junction entity.
    /// SQL: single query with two JOINs (ContractorWorkCategories → WorkCategories → Contractors).
    /// </summary>
    public async Task<IReadOnlyList<Contractor>> GetRecentlyQualifiedAsync(
        string categoryCode, DateTime since, CancellationToken ct = default)
        => await Set
            .Where(cwc => cwc.WorkCategory.Code == categoryCode
                       && cwc.QualificationDate >= since)
            .OrderByDescending(cwc => cwc.QualificationDate)
            .Select(cwc => cwc.Contractor)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ContractorWorkCategory>> GetByContractorAsync(
        int contractorId, CancellationToken ct = default)
        => await Set
            .Where(cwc => cwc.ContractorId == contractorId)
            .Include(cwc => cwc.WorkCategory)
            .AsNoTracking()
            .ToListAsync(ct);
}
