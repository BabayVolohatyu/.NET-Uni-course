using Microsoft.AspNetCore.Mvc;
using Tenders.Api.Metrics;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;

namespace Tenders.Api.Controllers;

/// <summary>
/// Thin controller for Tender lifecycle management.
/// Attribute routing, async all the way, CancellationToken forwarded to BLL and DAL.
/// No SQL, no business rules — validation is DataAnnotations on DTOs (pre-service)
/// and business rules are in TenderService.
/// </summary>
[ApiController]
[Route("api/tenders")]
[Produces("application/json")]
public class TendersController(ITenderService tenderService, TenderMetrics metrics) : ControllerBase
{
    /// <summary>Get a tender with its full details.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TenderWithDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenderWithDetailsDto>> GetById(long id, CancellationToken ct)
    {
        var tender = await metrics.MeasureAsync("get", () => tenderService.GetTenderByIdAsync(id, ct));
        return tender is null ? NotFound() : Ok(tender);
    }

    /// <summary>Get all tenders for a procuring entity, optionally filtered by status.</summary>
    [HttpGet("by-entity/{procuringEntityId:long}")]
    [ProducesResponseType(typeof(IReadOnlyList<TenderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenderDto>>> GetByEntity(
        long procuringEntityId,
        [FromQuery] string? status,
        CancellationToken ct)
        => Ok(await tenderService.GetTendersByEntityAsync(procuringEntityId, status, ct));

    /// <summary>Create a new tender in Draft status.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenderWithDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenderWithDetailsDto>> Create(
        CreateTenderDto dto, CancellationToken ct)
    {
        var created = await metrics.MeasureAsync("create", () => tenderService.CreateTenderAsync(dto, ct));
        metrics.TenderCreated(created.Status);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Change the tender's lifecycle status (e.g., Draft → Published → BiddingOpen → …).
    /// Returns 409 if the transition is not allowed; 404 if the tender does not exist.
    /// </summary>
    [HttpPost("{id:long}/change-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        long id, ChangeTenderStatusDto dto, CancellationToken ct)
    {
        await metrics.MeasureAsync(
            "status_change",
            () => tenderService.ChangeTenderStatusAsync(id, dto.NewStatus, dto.Reason, ct));
        metrics.TenderStatusChanged(dto.NewStatus);
        return NoContent();
    }

    /// <summary>Get the full status-change audit trail for a tender.</summary>
    [HttpGet("{id:long}/status-history")]
    [ProducesResponseType(typeof(IReadOnlyList<TenderStatusHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenderStatusHistoryDto>>> GetStatusHistory(
        long id, CancellationToken ct)
        => Ok(await tenderService.GetStatusHistoryAsync(id, ct));
}
