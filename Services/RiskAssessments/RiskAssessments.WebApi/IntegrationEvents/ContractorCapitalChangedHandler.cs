using Microsoft.Extensions.Logging;
using Platform.EventBus;

namespace RiskAssessments.WebApi.IntegrationEvents;

/// <summary>
/// Capital on a triggered flag is the value at detection time (a snapshot).
/// This handler records the event so a redelivery is idempotent and does not rewrite that snapshot.
/// </summary>
public sealed class ContractorCapitalChangedHandler(
    RiskCatalogSync sync,
    ILogger<ContractorCapitalChangedHandler> logger)
{
    public Task HandleAsync(ContractorCapitalChangedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, e.ContractorId, "lastCapitalEventAt", (_, _) =>
        {
            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "authorized capital {OldCapital} -> {NewCapital}. Flag authorizedCapital left unchanged (detection snapshot)",
                nameof(ContractorCapitalChangedEvent), e.EventId, e.CorrelationId, e.ContractorId,
                e.OldAuthorizedCapital, e.NewAuthorizedCapital);
            return Task.CompletedTask;
        }, ct);
}
