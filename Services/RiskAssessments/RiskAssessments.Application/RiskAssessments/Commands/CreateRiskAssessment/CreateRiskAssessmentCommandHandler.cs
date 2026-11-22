using MediatR;
using Microsoft.Extensions.Logging;
using Platform.EventBus;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Interfaces;
using RiskAssessments.Domain.ValueObjects;
using System.Diagnostics;

namespace RiskAssessments.Application.RiskAssessments.Commands.CreateRiskAssessment;

public sealed class CreateRiskAssessmentCommandHandler(
    IRiskAssessmentRepository assessments,
    IEventPublisher publisher,
    ICorrelationContext correlation,
    ILogger<CreateRiskAssessmentCommandHandler> logger)
    : IRequestHandler<CreateRiskAssessmentCommand, string>
{
    // ActivitySource for custom spans — visible in Aspire Dashboard "Traces"
    // ApplicationName = "risk-assessments-api" (registered in ServiceDefaults)
    private static readonly ActivitySource Source = new("RiskAssessments.WebApi");

    public async Task<string> Handle(CreateRiskAssessmentCommand cmd, CancellationToken ct)
    {
        using var activity = Source.StartActivity("CreateRiskAssessment");
        activity?.SetTag("tender.id", cmd.TenderId);
        activity?.SetTag("tender.title", cmd.TenderTitle);
        activity?.SetTag("risk.level", cmd.RiskLevel);
        activity?.SetTag("risk.score", cmd.OverallRiskScore);
        activity?.SetTag("procuring_entity.id", cmd.ProcuringEntityId);

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

        activity?.SetTag("assessment.id", assessment.Id.ToString());

        // The document is stored. Publish only after that insert succeeds.
        await publisher.PublishAsync(
            new RiskAssessmentRecordedEvent(
                assessment.Id.ToString(),
                cmd.TenderId,
                cmd.TenderTitle,
                cmd.RiskLevel,
                cmd.OverallRiskScore,
                cmd.ProcuringEntityId)
            {
                CorrelationId = correlation.Current
            },
            MessagingTopology.RiskAssessmentsExchange,
            MessagingTopology.AssessmentRecordedRoutingKey,
            ct);

        // Structured business event log — auditors track risk assessment creation by TenderId and RiskLevel
        logger.LogInformation(
            "Risk assessment created: AssessmentId={AssessmentId} TenderId={TenderId} " +
            "TenderTitle={TenderTitle} RiskLevel={RiskLevel} OverallRiskScore={OverallRiskScore} " +
            "ProcuringEntityId={ProcuringEntityId} AuditorId={AuditorId}",
            assessment.Id, cmd.TenderId, cmd.TenderTitle,
            cmd.RiskLevel, cmd.OverallRiskScore,
            cmd.ProcuringEntityId, cmd.AuditorId);

        return assessment.Id.ToString();
    }
}
