using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;

namespace Catalog.Dal;

/// <summary>
/// Unit of Work implementation for the Contractor Registry context.
///
/// All repositories share the same CatalogDbContext instance so their EF change-tracker
/// state is pooled. When SaveChangesAsync is called once at the end of a business operation,
/// all pending inserts, updates, and deletes flush to the database in ONE transaction.
///
/// Repositories never call SaveChangesAsync themselves — they only stage changes.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Contractors = new ContractorRepository(context);
        WorkCategories = new WorkCategoryRepository(context);
        ContractorWorkCategories = new ContractorWorkCategoryRepository(context);
    }

    public IContractorRepository Contractors { get; }
    public IWorkCategoryRepository WorkCategories { get; }
    public IContractorWorkCategoryRepository ContractorWorkCategories { get; }

    /// <summary>
    /// Flushes all staged changes from ALL repositories in a single DB round-trip / transaction.
    /// Call this ONCE at the end of each business operation from the BLL service.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public void Dispose() => _context.Dispose();
}
