using Microsoft.AspNetCore.Mvc;
using Tenders.Api.Metrics;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;

namespace Tenders.Api.Controllers;

/// <summary>
/// Thin controller for Bid operations (submit, list, award).
/// No business logic — delegates entirely to IBidService.
/// </summary>
[ApiController]
[Route("api/tenders/{tenderId:long}/bids")]
[Produces("application/json")]
public class BidsController(IBidService bidService, TenderMetrics metrics) : ControllerBase
{
    /// <summary>Get all bids for a tender sorted by ProposedPrice ASC.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BidDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> GetBids(long tenderId, CancellationToken ct)
        => Ok(await bidService.GetBidsForTenderAsync(tenderId, ct));

    /// <summary>
    /// Submit a bid for an open tender.
    /// Returns 409 if the tender is not in BiddingOpen status.
    /// Returns 404 if the tender or contractor does not exist.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BidDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BidDto>> SubmitBid(
        long tenderId, SubmitBidDto dto, CancellationToken ct)
    {
        var created = await metrics.MeasureAsync(
            "submit_bid",
            () => bidService.SubmitBidAsync(tenderId, dto, ct));
        metrics.BidSubmitted();
        return Created($"api/tenders/{tenderId}/bids", created);
    }

    /// <summary>
    /// Award the tender to the specified contractor.
    /// Returns 409 if the tender is not in EvaluationInProgress, bid doesn't exist, or is disqualified.
    /// Transactional: awards the bid AND changes tender status atomically.
    /// </summary>
    [HttpPost("award")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AwardTender(
        long tenderId, AwardTenderDto dto, CancellationToken ct)
    {
        await metrics.MeasureAsync(
            "award",
            () => bidService.AwardTenderAsync(tenderId, dto, ct));
        metrics.TenderAwarded();
        return NoContent();
    }
}
