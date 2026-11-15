using Microsoft.AspNetCore.Mvc;
using RiskAssessments.Application.AuditNotes.Commands.CreateAuditNote;
using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.AuditNotes.Queries.GetAuditNotesByAssessment;
using RiskAssessments.Application.Common.Models;

namespace RiskAssessments.WebApi.Controllers;

[Route("api/risk-assessments/{assessmentId}/notes")]
public sealed class AuditNotesController(MediatR.ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AuditNoteDto>>> GetList(
        string assessmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetAuditNotesByAssessmentQuery(assessmentId, page, pageSize), ct));

    [HttpPost]
    [ProducesResponseType(typeof(AuditNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuditNoteDto>> Create(
        string assessmentId,
        CreateAuditNoteBody body,
        CancellationToken ct)
    {
        var note = await Mediator.Send(new CreateAuditNoteCommand(
            assessmentId,
            body.AuthorId,
            body.AuthorName,
            body.Text,
            body.NoteType,
            body.IsPinned,
            body.AttachmentName,
            body.AttachmentUrl), ct);

        return Created($"/api/risk-assessments/{assessmentId}/notes", note);
    }
}

public sealed record CreateAuditNoteBody(
    string AuthorId,
    string AuthorName,
    string Text,
    string NoteType,
    bool IsPinned,
    string? AttachmentName,
    string? AttachmentUrl);
