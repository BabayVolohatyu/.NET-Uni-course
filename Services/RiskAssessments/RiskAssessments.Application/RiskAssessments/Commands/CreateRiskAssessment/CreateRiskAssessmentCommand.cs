using RiskAssessments.Application.Common.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.CreateRiskAssessment;

public sealed record CreateRiskAssessmentCommand(
    long TenderId,
    string TenderTitle,
    long ProcuringEntityId,
    string ProcuringEntityName,
    string AuditorId,
    string AuditorName,
    double OverallRiskScore,
    string RiskLevel,
    decimal? ExpectedBudgetAmount,
    string? ExpectedBudgetCurrency) : ICommand<string>;
