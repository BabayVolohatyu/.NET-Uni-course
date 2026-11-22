using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>
/// Async Generic Repository backed by EF Core DbSet&lt;T&gt;.
///
/// Convention:
///   - GetByIdAsync / GetAllAsync / AddAsync are async (they hit the DB).
///   - Update / Delete are synchronous (they only mark entities in the EF change-tracker;
///     the actual SQL UPDATE/DELETE runs when SaveChangesAsync is called on IUnitOfWork).
/// </summary>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(CatalogDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    /// <summary>Marks the entity as Modified in the change-tracker. Does NOT save.</summary>
    public void Update(T entity) => Set.Update(entity);

    /// <summary>Marks the entity for removal in the change-tracker. Does NOT save.</summary>
    public void Delete(T entity) => Set.Remove(entity);
}
