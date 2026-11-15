namespace Catalog.Dal.Interfaces;

/// <summary>
/// Async Generic Repository contract — same surface as Lab 02 Dapper version
/// but backed by EF Core DbSet&lt;T&gt;.
///
/// Convention:
///   - Methods that query the DB are async (Async suffix, return Task).
///   - Update / Delete only change the EF change-tracker state — they are synchronous.
///   - SaveChangesAsync is on IUnitOfWork, NOT here — repositories never flush on their own.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}
