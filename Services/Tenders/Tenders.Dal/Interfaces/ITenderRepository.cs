using Tenders.Domain.Models;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Domain-specific tender operations on top of the generic CRUD contract.
/// </summary>
public interface ITenderRepository : IGenericRepository<Tender>
{
    /// <summary>Loads a tender together with its TenderDetails via a single LEFT JOIN query (Dapper multi-mapping).</summary>
    Task<Tender?> GetWithDetailsAsync(long tenderId, CancellationToken ct = default);

    /// <summary>Returns all non-deleted tenders for a given procuring entity, optionally filtered by status.</summary>
    Task<IReadOnlyList<Tender>> GetByProcuringEntityAsync(long procuringEntityId, string? status = null, CancellationToken ct = default);

    /// <summary>Transitions a tender to a new status via dbo.usp_ChangeTenderStatus stored procedure.</summary>
    Task ChangeStatusAsync(long tenderId, string newStatus, string? changedBy = null, string? reason = null, CancellationToken ct = default);

    /// <summary>Returns the status-change audit log for a tender.</summary>
    Task<IReadOnlyList<TenderStatusHistory>> GetStatusHistoryAsync(long tenderId, CancellationToken ct = default);
}
