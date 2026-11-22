using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;
using Tenders.Domain.Exceptions;

namespace Tenders.Api.Controllers;

/// <summary>
/// Thin controller for ProcuringEntity CRUD.
/// No SQL, no business rules — only HTTP contract (routes, status codes, CancellationToken forwarding).
/// </summary>
[ApiController]
[Authorize]
[Route("api/procuring-entities")]
[Produces("application/json")]
public class ProcuringEntitiesController(ITenderService tenderService) : ControllerBase
{
    /// <summary>Get all procuring entities.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProcuringEntityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProcuringEntityDto>>> GetAll(CancellationToken ct)
        => Ok(await tenderService.GetAllProcuringEntitiesAsync(ct));

    /// <summary>Get a single procuring entity by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProcuringEntityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcuringEntityDto>> GetById(long id, CancellationToken ct)
    {
        var entity = await tenderService.GetProcuringEntityByIdAsync(id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }

    /// <summary>Create a new procuring entity.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProcuringEntityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProcuringEntityDto>> Create(
        CreateProcuringEntityDto dto, CancellationToken ct)
    {
        var created = await tenderService.CreateProcuringEntityAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
