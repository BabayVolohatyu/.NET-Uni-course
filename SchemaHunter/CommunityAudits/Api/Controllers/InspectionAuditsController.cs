using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SchemaHunter.CommunityAudits.Domain.Documents;
using SchemaHunter.CommunityAudits.Infrastructure.Repositories;

namespace SchemaHunter.CommunityAudits.Api.Controllers;

/// <summary>
/// CRUD and operational endpoints for inspection audits (Context 3).
/// </summary>
[ApiController]
[Route("api/inspection-audits")]
[Produces("application/json")]
public sealed class InspectionAuditsController : ControllerBase
{
    private readonly InspectionAuditRepository _audits;

    public InspectionAuditsController(InspectionAuditRepository audits) => _audits = audits;

    // GET /api/inspection-audits?status=Verified&region=Kyiv%20Oblast&skip=0&limit=20
    /// <summary>Returns a paginated list of audits filtered by optional status and region.</summary>
    [HttpGet]
    [ProducesResponseType<List<InspectionAudit>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? status = null,
        [FromQuery] string? region = null,
        [FromQuery] int     skip   = 0,
        [FromQuery] int     limit  = 20,
        CancellationToken   ct     = default)
    {
        var page = await _audits.GetPagedAsync(status, region, skip, limit, ct);
        return Ok(page);
    }

    // GET /api/inspection-audits/by-code/{auditCode}
    /// <summary>Returns a single audit by its human-readable code.</summary>
    [HttpGet("by-code/{auditCode}")]
    [ProducesResponseType<InspectionAudit>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string auditCode, CancellationToken ct = default)
    {
        var audit = await _audits.GetByAuditCodeAsync(auditCode, ct);
        return audit is null ? NotFound() : Ok(audit);
    }

    // GET /api/inspection-audits/by-project/{assetCode}
    /// <summary>Returns all audits linked to a given project asset code.</summary>
    [HttpGet("by-project/{assetCode}")]
    [ProducesResponseType<List<InspectionAudit>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(string assetCode, CancellationToken ct = default)
    {
        var audits = await _audits.GetByProjectAsync(assetCode, ct);
        return Ok(audits);
    }

    // POST /api/inspection-audits
    /// <summary>Creates a new inspection audit document.</summary>
    [HttpPost]
    [ProducesResponseType<InspectionAudit>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAuditRequest req,
        CancellationToken ct = default)
    {
        var audit = new InspectionAudit
        {
            Id               = ObjectId.GenerateNewId().ToString(),
            AuditCode        = req.AuditCode,
            ProjectReference = req.ProjectReference,
            TenderReference  = req.TenderReference,
            AuditorOrg       = req.AuditorOrg,
            Status           = req.Status ?? "Draft",
            OverallSeverity  = req.OverallSeverity ?? "None",
            Findings         = req.Findings ?? new(),
            EvidenceAttachments = new(),
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        await _audits.InsertAsync(audit, ct);
        return CreatedAtAction(nameof(GetByCode), new { auditCode = audit.AuditCode }, audit);
    }

    // PATCH /api/inspection-audits/{auditId}/findings/{findingId}/resolve
    /// <summary>Marks a specific finding as resolved.</summary>
    [HttpPatch("{auditId}/findings/{findingId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveFinding(
        string auditId, string findingId, CancellationToken ct = default)
    {
        await _audits.MarkFindingResolvedAsync(auditId, findingId, ct);
        return NoContent();
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

/// <summary>Payload for <c>POST /api/inspection-audits</c>.</summary>
public record CreateAuditRequest(
    string             AuditCode,
    ProjectReference   ProjectReference,
    AuditorOrg         AuditorOrg,
    TenderReference?   TenderReference   = null,
    string?            Status            = "Draft",
    string?            OverallSeverity   = "None",
    List<AuditFinding>? Findings         = null);
