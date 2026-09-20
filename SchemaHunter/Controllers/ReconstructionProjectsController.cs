using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchemaHunter.InfrastructureRegistry.Persistence;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.Controllers;

/// <summary>
/// CRUD operations for reconstruction projects (Context 2 — Infrastructure Registry).
/// </summary>
[ApiController]
[Route("api/reconstruction-projects")]
[Produces("application/json")]
public sealed class ReconstructionProjectsController : ControllerBase
{
    private readonly InfrastructureDbContext _db;

    public ReconstructionProjectsController(InfrastructureDbContext db) => _db = db;

    // GET /api/reconstruction-projects?pageSize=20&afterId=0&status=Active
    /// <summary>Returns a keyset-paginated list of reconstruction projects.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReconstructionProject>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? status    = null,
        [FromQuery] long    afterId   = 0,
        [FromQuery] int     pageSize  = 20,
        CancellationToken   ct        = default)
    {
        var query = _db.ReconstructionProjects
            .Include(p => p.AssetCategory)
            .Include(p => p.Passport)
            .Include(p => p.FundingSources)
            .Where(p => p.Id > afterId)
            .OrderBy(p => p.Id);

        if (!string.IsNullOrWhiteSpace(status))
            query = (IOrderedQueryable<ReconstructionProject>)query.Where(p => p.Status == status);

        var page = await query.Take(pageSize).ToListAsync(ct);
        return Ok(page);
    }

    // GET /api/reconstruction-projects/{id}
    /// <summary>Returns a single project by its internal ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ReconstructionProject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct = default)
    {
        var project = await _db.ReconstructionProjects
            .Include(p => p.AssetCategory)
            .Include(p => p.Passport)
            .Include(p => p.FundingSources)
            .Include(p => p.ProjectClassificationTags)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project is null ? NotFound() : Ok(project);
    }

    // GET /api/reconstruction-projects/by-asset-code/{assetCode}
    /// <summary>Looks up a project by its cross-context asset code.</summary>
    [HttpGet("by-asset-code/{assetCode}")]
    [ProducesResponseType<ReconstructionProject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAssetCode(string assetCode, CancellationToken ct = default)
    {
        var project = await _db.ReconstructionProjects
            .Include(p => p.AssetCategory)
            .FirstOrDefaultAsync(p => p.AssetCode == assetCode, ct);

        return project is null ? NotFound() : Ok(project);
    }

    // POST /api/reconstruction-projects
    /// <summary>Creates a new reconstruction project.</summary>
    [HttpPost]
    [ProducesResponseType<ReconstructionProject>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest req,
        CancellationToken ct = default)
    {
        var project = new ReconstructionProject
        {
            AssetCode        = req.AssetCode,
            Name             = req.Name,
            AssetCategoryId  = req.AssetCategoryId,
            AllocatedBudget  = req.AllocatedBudget,
            Status           = req.Status,
            Region           = req.Region,
            CreatedAt        = DateTime.UtcNow
        };

        _db.ReconstructionProjects.Add(project);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    // PUT /api/reconstruction-projects/{id}
    /// <summary>Updates mutable fields of an existing project.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType<ReconstructionProject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateProjectRequest req,
        CancellationToken ct = default)
    {
        var project = await _db.ReconstructionProjects.FindAsync([id], ct);
        if (project is null) return NotFound();

        project.Name            = req.Name;
        project.AllocatedBudget = req.AllocatedBudget;
        project.Status          = req.Status;
        project.Region          = req.Region;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "The record was modified by another request. Reload and retry." });
        }

        return Ok(project);
    }

    // DELETE /api/reconstruction-projects/{id}
    /// <summary>Soft-deletes a project (sets IsDeleted = true).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(long id, CancellationToken ct = default)
    {
        var project = await _db.ReconstructionProjects.FindAsync([id], ct);
        if (project is null) return NotFound();

        project.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

/// <summary>Payload for <c>POST /api/reconstruction-projects</c>.</summary>
public record CreateProjectRequest(
    string  AssetCode,
    string  Name,
    int     AssetCategoryId,
    decimal AllocatedBudget,
    string  Status,
    string  Region);

/// <summary>Payload for <c>PUT /api/reconstruction-projects/{id}</c>.</summary>
public record UpdateProjectRequest(
    string  Name,
    decimal AllocatedBudget,
    string  Status,
    string  Region);
