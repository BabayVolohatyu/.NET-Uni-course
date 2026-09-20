using Microsoft.AspNetCore.Mvc;
using SchemaHunter.Procurement.Domain.Commands;
using SchemaHunter.Procurement.Domain.DTOs;
using SchemaHunter.Procurement.Infrastructure.Repositories;

namespace SchemaHunter.Procurement.Api.Controllers;

/// <summary>
/// Manages tenders imported from the Prozorro public procurement API.
/// </summary>
[ApiController]
[Route("api/tenders")]
[Produces("application/json")]
public sealed class TendersController : ControllerBase
{
    private readonly TenderRepository _tenders;

    public TendersController(TenderRepository tenders) => _tenders = tenders;

    // GET /api/tenders?status=Active&projectCode=ROAD-UA-001&afterTenderId=0&pageSize=20
    /// <summary>Returns a keyset-paginated list of tenders with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TenderPageItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string?  status        = null,
        [FromQuery] string?  projectCode   = null,
        [FromQuery] long     afterTenderId = 0,
        [FromQuery] int      pageSize      = 20,
        CancellationToken    ct            = default)
    {
        var page = await _tenders.GetPagedAsync(status, projectCode, afterTenderId, pageSize, ct);
        return Ok(page);
    }

    // POST /api/tenders
    /// <summary>Creates or updates a tender by its Prozorro ID (upsert).</summary>
    [HttpPost]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertTenderCommand cmd,
        CancellationToken ct = default)
    {
        var tenderId = await _tenders.UpsertAsync(cmd, ct);
        return Ok(new { tenderId });
    }

    // POST /api/tenders/{tenderId}/bids
    /// <summary>Submits a bid for a tender and recalculates supplier risk.</summary>
    [HttpPost("{tenderId:long}/bids")]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitBid(
        long tenderId,
        [FromBody] SubmitBidCommand cmd,
        CancellationToken ct = default)
    {
        // Ensure route-level tenderId matches body
        var effectiveCmd = cmd with { TenderId = tenderId };
        try
        {
            var bidId = await _tenders.SubmitBidAsync(effectiveCmd, ct);
            return Ok(new { bidId });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50001 || ex.Number == 50002)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST /api/tenders/{tenderId}/risk-assessment
    /// <summary>Runs the rule engine and persists a risk assessment for the tender.</summary>
    [HttpPost("{tenderId:long}/risk-assessment")]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ComputeRisk(
        long tenderId,
        CancellationToken ct = default)
    {
        try
        {
            var riskAssessmentId = await _tenders.ComputeRiskAsync(tenderId, ct);
            return Ok(new { riskAssessmentId });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50004)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // GET /api/tenders/risk-report/{projectCode}
    /// <summary>Returns aggregated risk summary and tender detail rows for a project.</summary>
    [HttpGet("risk-report/{projectCode}")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskReport(
        string projectCode,
        CancellationToken ct = default)
    {
        var (summary, tenders) = await _tenders.GetRiskReportAsync(projectCode, ct);
        return Ok(new { summary, tenders });
    }
}
