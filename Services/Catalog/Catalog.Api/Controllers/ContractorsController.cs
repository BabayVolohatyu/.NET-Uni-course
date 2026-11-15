using Catalog.Bll.DTOs;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Queries;
using Catalog.Bll.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>
/// REST API for Contractor Registry.
/// Controllers are thin orchestrators: validate input → call service → return HTTP response.
/// No business logic, no DbContext, no EF Core references here.
///
/// Response codes:
///   200 OK          — GET list, GET by id
///   201 Created     — POST (with Location header)
///   204 No Content  — PUT, DELETE, PATCH activate/deactivate
///   400 Bad Request — validation failure (ValidationProblemDetails)
///   404 Not Found   — resource not found (ProblemDetails)
///   409 Conflict    — duplicate EDRPOU or domain rule violation (ProblemDetails)
/// </summary>
[ApiController]
[Route("api/contractors")]
[Produces("application/json")]
public sealed class ContractorsController(IContractorService service) : ControllerBase
{
    // GET api/contractors?page=1&pageSize=20&search=&sortBy=name&sortDir=asc
    [HttpGet]
    [ProducesResponseType<PagedResult<ContractorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ContractorDto>>> GetList(
        [FromQuery] ContractorQuery query,
        CancellationToken ct)
        => Ok(await service.GetListAsync(query, ct));

    // GET api/contractors/5
    [HttpGet("{id:int}", Name = nameof(GetById))]
    [ProducesResponseType<ContractorWithDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractorWithDetailsDto>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    // POST api/contractors
    [HttpPost]
    [ProducesResponseType<ContractorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractorDto>> Create(
        [FromBody] CreateContractorDto input,
        [FromServices] IValidator<CreateContractorDto> validator,
        CancellationToken ct)
    {
        // Explicit FluentValidation call (not via deprecated AddFluentValidation middleware)
        var result = await validator.ValidateAsync(input, ct);
        if (!result.IsValid)
            return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));

        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT api/contractors/5
    [HttpPut("{id:int}")]
    [ProducesResponseType<ContractorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractorDto>> Update(
        int id,
        [FromBody] UpdateContractorDto input,
        [FromServices] IValidator<UpdateContractorDto> validator,
        CancellationToken ct)
    {
        var result = await validator.ValidateAsync(input, ct);
        if (!result.IsValid)
            return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));

        var updated = await service.UpdateAsync(id, input, ct);
        return Ok(updated);
    }

    // DELETE api/contractors/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    // PATCH api/contractors/5/activate
    [HttpPatch("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        await service.ActivateAsync(id, ct);
        return NoContent();
    }

    // PATCH api/contractors/5/deactivate
    [HttpPatch("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await service.DeactivateAsync(id, ct);
        return NoContent();
    }
}
