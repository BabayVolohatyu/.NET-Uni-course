using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.RiskAssessments.Commands.AddTriggeredFlag;

public sealed record AddTriggeredFlagCommand(
    string AssessmentId,
    string RuleCode,
    string Severity,
    string Description,
    string? AffectedField,
    long? ContractorId,
    int? CompanyAgeDays,
    decimal? AuthorizedCapital,
    int ExpectedVersion) : ICommand<RiskAssessmentDetailsDto>;
