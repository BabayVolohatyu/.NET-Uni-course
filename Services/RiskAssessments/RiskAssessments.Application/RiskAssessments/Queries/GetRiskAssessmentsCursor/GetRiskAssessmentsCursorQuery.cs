using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsCursor;

public sealed record GetRiskAssessmentsCursorQuery(
    long? ProcuringEntityId,
    string? AfterId,
    int PageSize = 20) : IQuery<CursorPage<RiskAssessmentDto>>;
