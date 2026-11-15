using MediatR;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Interfaces;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Application.RiskAssessments.Commands.CreateRiskAssessment;

public sealed class CreateRiskAssessmentCommandHandler(IRiskAssessmentRepository assessments)
    : IRequestHandler<CreateRiskAssessmentCommand, string>
{
    public async Task<string> Handle(CreateRiskAssessmentCommand cmd, CancellationToken ct)
    {
        var level = Enum.Parse<RiskLevel>(cmd.RiskLevel, ignoreCase: true);
        Money? budget = cmd.ExpectedBudgetAmount is decimal amount
            ? new Money(amount, string.IsNullOrWhiteSpace(cmd.ExpectedBudgetCurrency) ? "UAH" : cmd.ExpectedBudgetCurrency)
            : null;

        var assessment = new RiskAssessment(
            cmd.TenderId,
            cmd.TenderTitle,
            cmd.ProcuringEntityId,
            cmd.ProcuringEntityName,
            new Auditor(cmd.AuditorId, cmd.AuditorName),
            cmd.OverallRiskScore,
            level,
            budget);

        await assessments.AddAsync(assessment, ct);
        return assessment.Id.ToString();
    }
}
