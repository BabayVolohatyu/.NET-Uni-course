using Microsoft.Extensions.Logging;
using Platform.EventBus;

namespace RiskAssessments.WebApi.IntegrationEvents;

public sealed class ContractorRegisteredHandler(
    RiskCatalogSync sync,
    ILogger<ContractorRegisteredHandler> logger)
{
    public Task HandleAsync(ContractorRegisteredEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, e.ContractorId, "lastNameEventAt", (_, _) =>
        {
            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} registered as {ContractorName}. No assessment document to rewrite yet",
                nameof(ContractorRegisteredEvent), e.EventId, e.CorrelationId, e.ContractorId, e.ContractorName);
            return Task.CompletedTask;
        }, ct);
}
