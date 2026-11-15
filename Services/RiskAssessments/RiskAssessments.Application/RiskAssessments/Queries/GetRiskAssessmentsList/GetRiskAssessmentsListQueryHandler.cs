using MediatR;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsList;

public sealed class GetRiskAssessmentsListQueryHandler(IRiskAssessmentQueries queries)
    : IRequestHandler<GetRiskAssessmentsListQuery, PagedResult<RiskAssessmentDto>>
{
    public Task<PagedResult<RiskAssessmentDto>> Handle(GetRiskAssessmentsListQuery query, CancellationToken ct)
        => queries.GetListAsync(
            query.ProcuringEntityId,
            query.RiskLevel,
            query.Page,
            Math.Clamp(query.PageSize, 1, 100),
            ct);
}
