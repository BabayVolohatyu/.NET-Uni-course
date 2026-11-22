using MediatR;
using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;

namespace RiskAssessments.Application.AuditNotes.Queries.GetAuditNotesByAssessment;

public sealed class GetAuditNotesByAssessmentQueryHandler(IAuditNoteQueries queries)
    : IRequestHandler<GetAuditNotesByAssessmentQuery, PagedResult<AuditNoteDto>>
{
    public Task<PagedResult<AuditNoteDto>> Handle(GetAuditNotesByAssessmentQuery query, CancellationToken ct)
        => queries.GetByAssessmentAsync(
            query.RiskAssessmentId,
            query.Page,
            Math.Clamp(query.PageSize, 1, 100),
            ct);
}
