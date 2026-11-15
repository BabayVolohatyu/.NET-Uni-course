using Tenders.Domain.Models;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Domain-specific bid operations.
/// Bids have a composite primary key (TenderId, ContractorId), so IGenericRepository&lt;Bid&gt;
/// is extended with composite-key lookups and procedure calls.
/// </summary>
public interface IBidRepository : IGenericRepository<Bid>
{
    /// <summary>Returns all valid bids for a tender sorted by ProposedPrice ASC (uses dbo.usp_GetBidsForTender).</summary>
    Task<IReadOnlyList<Bid>> GetBidsForTenderAsync(long tenderId, CancellationToken ct = default);

    /// <summary>Submits or updates a bid via dbo.usp_SubmitBid stored procedure.</summary>
    Task SubmitBidAsync(long tenderId, long contractorId, decimal proposedPrice, decimal priceReductionPct = 0, string? createdBy = null, CancellationToken ct = default);

    /// <summary>Awards the tender to the specified winning contractor via dbo.usp_AwardTender.</summary>
    Task AwardTenderAsync(long tenderId, long winnerContractorId, string? changedBy = null, CancellationToken ct = default);
}
