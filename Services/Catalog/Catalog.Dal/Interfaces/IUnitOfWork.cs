namespace Catalog.Dal.Interfaces;

/// <summary>
/// Unit of Work: wraps all repositories for the Contractor Registry context.
/// SaveChangesAsync is called ONCE at the end of a business operation —
/// all repository operations (Add/Update/Delete) only mutate the EF change-tracker;
/// the single SaveChangesAsync flushes everything in one DB round-trip / transaction.
///
/// Compare with eShop: OrderingContext implements IUnitOfWork directly
/// (https://github.com/dotnet/eShop/tree/main/src/Ordering.Domain/SeedWork).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IContractorRepository Contractors { get; }
    IWorkCategoryRepository WorkCategories { get; }
    IContractorWorkCategoryRepository ContractorWorkCategories { get; }

    /// <summary>Flushes all pending changes across ALL repositories in one DB transaction.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
