using Tenders.Domain.Models;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Repository for ProcuringEntity — intentionally implemented in pure ADO.NET (no Generic Repository inheritance).
/// This demonstrates low-level SqlConnection + SqlCommand usage without an ORM or micro-ORM helper.
/// </summary>
public interface IProcuringEntityRepository
{
    Task<ProcuringEntity?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ProcuringEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ProcuringEntity?> GetByEDRPOUAsync(string edrpou, CancellationToken ct = default);
    Task<long> AddAsync(ProcuringEntity entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(ProcuringEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
