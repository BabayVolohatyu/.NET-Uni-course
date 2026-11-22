using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsList;

public sealed record GetRiskAssessmentsListQuery(
    long? ProcuringEntityId,
    string? RiskLevel,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<RiskAssessmentDto>>;
