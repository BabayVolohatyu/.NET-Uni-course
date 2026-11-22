using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Commands.UpdateRiskAssessment;

public sealed record UpdateRiskAssessmentCommand(
    string Id,
    double OverallRiskScore,
    string RiskLevel,
    int ExpectedVersion) : ICommand<RiskAssessmentDetailsDto>;
