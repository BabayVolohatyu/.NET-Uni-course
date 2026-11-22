using MediatR;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.SearchRiskAssessments;

public sealed class SearchRiskAssessmentsQueryHandler(IRiskAssessmentQueries queries)
    : IRequestHandler<SearchRiskAssessmentsQuery, PagedResult<RiskAssessmentDto>>
{
    public Task<PagedResult<RiskAssessmentDto>> Handle(SearchRiskAssessmentsQuery query, CancellationToken ct)
        => queries.SearchAsync(query.Text, query.Page, Math.Clamp(query.PageSize, 1, 100), ct);
}
