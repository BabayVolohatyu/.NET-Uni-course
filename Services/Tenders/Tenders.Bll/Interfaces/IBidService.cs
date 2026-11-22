using Tenders.Bll.Dtos;

namespace Tenders.Bll.Interfaces;

/// <summary>
/// BLL service for Bid operations.
/// </summary>
public interface IBidService
{
    Task<IReadOnlyList<BidDto>> GetBidsForTenderAsync(long tenderId, CancellationToken ct = default);

    /// <summary>
    /// Submits a bid for an open tender.
    /// BLL checks that the tender is in BiddingOpen status and validates price > 0.
    /// </summary>
    Task<BidDto> SubmitBidAsync(long tenderId, SubmitBidDto dto, CancellationToken ct = default);

    /// <summary>
    /// Awards the tender to the winning contractor.
    /// Transactional: marks the bid as winner AND changes tender status to Awarded atomically.
    /// UoW coordinates both repositories; rollback on failure returns both to EvaluationInProgress.
    /// </summary>
    Task AwardTenderAsync(long tenderId, AwardTenderDto dto, CancellationToken ct = default);
}
