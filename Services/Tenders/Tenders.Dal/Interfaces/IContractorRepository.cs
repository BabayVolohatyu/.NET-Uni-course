using Tenders.Domain.Models;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Repository for the local Contractor snapshot (plain read-model; no FK to Registry DB).
/// </summary>
public interface IContractorRepository : IGenericRepository<Contractor>
{
    Task<Contractor?> GetByEDRPOUAsync(string edrpou, CancellationToken ct = default);

    /// <summary>
    /// Upserts the local directory row from a catalog event.
    /// Does not update <c>Bids</c> — contractor name, EDRPOU, and proposed price on a bid are snapshots.
    /// Returns 0 when <paramref name="occurredAt"/> is older than the timestamp of that field.
    /// </summary>
    Task<int> ApplyCatalogSnapshotAsync(
        long id,
        string name,
        string edrpou,
        decimal? authorizedCapital,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the local directory row only when no bid references it.
    /// Returns the number of rows deleted (0 when bids still point at the contractor).
    /// </summary>
    Task<int> TryRemoveUnreferencedAsync(long id, CancellationToken ct = default);
}
