namespace Tenders.Dal.Interfaces;

/// <summary>
/// Generic CRUD contract shared by all Dapper-based repositories.
/// Concrete repository interfaces inherit this and add only domain-specific queries.
/// Methods return domain models or DTOs — never SqlDataReader, DataTable, or raw SQL strings.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
