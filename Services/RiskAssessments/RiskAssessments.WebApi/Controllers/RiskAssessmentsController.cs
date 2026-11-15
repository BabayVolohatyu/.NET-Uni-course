using Microsoft.AspNetCore.Mvc;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Commands.AddTriggeredFlag;
using RiskAssessments.Application.RiskAssessments.Commands.CreateRiskAssessment;
using RiskAssessments.Application.RiskAssessments.Commands.DeleteRiskAssessment;
using RiskAssessments.Application.RiskAssessments.Commands.UpdateRiskAssessment;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentById;
using RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsCursor;
using RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsList;
using RiskAssessments.Application.RiskAssessments.Queries.SearchRiskAssessments;

namespace RiskAssessments.WebApi.Controllers;

[Route("api/risk-assessments")]
public sealed class RiskAssessmentsController(MediatR.ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RiskAssessmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RiskAssessmentDto>>> GetList(
        [FromQuery] long? procuringEntityId,
        [FromQuery] string? riskLevel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetRiskAssessmentsListQuery(procuringEntityId, riskLevel, page, pageSize), ct));

    [HttpGet("cursor")]
    [ProducesResponseType(typeof(CursorPage<RiskAssessmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CursorPage<RiskAssessmentDto>>> GetCursor(
        [FromQuery] long? procuringEntityId,
        [FromQuery] string? afterId,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetRiskAssessmentsCursorQuery(procuringEntityId, afterId, pageSize), ct));

    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<RiskAssessmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RiskAssessmentDto>>> Search(
        [FromQuery] string text,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new SearchRiskAssessmentsQuery(text, page, pageSize), ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RiskAssessmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskAssessmentDetailsDto>> GetById(string id, CancellationToken ct)
    {
        var assessment = await Mediator.Send(new GetRiskAssessmentByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{assessment.Version}\"";
        return Ok(assessment);
    }

    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<string>> Create(CreateRiskAssessmentCommand command, CancellationToken ct)
    {
        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RiskAssessmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RiskAssessmentDetailsDto>> Update(
        string id,
        UpdateRiskAssessmentBody body,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var updated = await Mediator.Send(
            new UpdateRiskAssessmentCommand(id, body.OverallRiskScore, body.RiskLevel, ReadVersion(ifMatch)),
            ct);
        Response.Headers.ETag = $"\"{updated.Version}\"";
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        await Mediator.Send(new DeleteRiskAssessmentCommand(id, ReadVersion(ifMatch)), ct);
        return NoContent();
    }

    [HttpPost("{id}/flags")]
    [ProducesResponseType(typeof(RiskAssessmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RiskAssessmentDetailsDto>> AddFlag(
        string id,
        AddTriggeredFlagBody body,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var updated = await Mediator.Send(new AddTriggeredFlagCommand(
            id,
            body.RuleCode,
            body.Severity,
            body.Description,
            body.AffectedField,
            body.ContractorId,
            body.CompanyAgeDays,
            body.AuthorizedCapital,
            ReadVersion(ifMatch)), ct);
        Response.Headers.ETag = $"\"{updated.Version}\"";
        return Ok(updated);
    }
}

public sealed record UpdateRiskAssessmentBody(double OverallRiskScore, string RiskLevel);

public sealed record AddTriggeredFlagBody(
    string RuleCode,
    string Severity,
    string Description,
    string? AffectedField,
    long? ContractorId,
    int? CompanyAgeDays,
    decimal? AuthorizedCapital);
