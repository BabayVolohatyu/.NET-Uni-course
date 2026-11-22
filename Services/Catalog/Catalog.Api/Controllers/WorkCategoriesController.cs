using Catalog.Bll.DTOs;
using Microsoft.AspNetCore.Authorization;
using Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>
/// REST API for WorkCategory reference data.
/// Reference data is seeded via migrations and is read-only through this controller.
/// </summary>
[ApiController]
[Authorize]
[Route("api/work-categories")]
[Produces("application/json")]
public sealed class WorkCategoriesController(IWorkCategoryService service) : ControllerBase
{
    // GET api/work-categories
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkCategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    // GET api/work-categories/3
    [HttpGet("{id:int}")]
    [ProducesResponseType<WorkCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkCategoryDto>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    // GET api/work-categories/by-code/ROAD
    [HttpGet("by-code/{code}")]
    [ProducesResponseType<WorkCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkCategoryDto>> GetByCode(string code, CancellationToken ct)
        => Ok(await service.GetByCodeAsync(code, ct));
}
