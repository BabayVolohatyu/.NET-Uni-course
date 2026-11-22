using RiskAssessments.Application.Common.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.DeleteRiskAssessment;

public sealed record DeleteRiskAssessmentCommand(string Id, int ExpectedVersion) : ICommand;
