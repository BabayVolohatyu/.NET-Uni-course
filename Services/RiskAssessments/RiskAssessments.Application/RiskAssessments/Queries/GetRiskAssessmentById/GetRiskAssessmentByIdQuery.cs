using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentById;

public sealed record GetRiskAssessmentByIdQuery(string Id) : IQuery<RiskAssessmentDetailsDto>;
