using MediatR;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsCursor;

public sealed class GetRiskAssessmentsCursorQueryHandler(IRiskAssessmentQueries queries)
    : IRequestHandler<GetRiskAssessmentsCursorQuery, CursorPage<RiskAssessmentDto>>
{
    public Task<CursorPage<RiskAssessmentDto>> Handle(GetRiskAssessmentsCursorQuery query, CancellationToken ct)
        => queries.GetCursorPageAsync(
            query.ProcuringEntityId,
            query.AfterId,
            Math.Clamp(query.PageSize, 1, 100),
            ct);
}
