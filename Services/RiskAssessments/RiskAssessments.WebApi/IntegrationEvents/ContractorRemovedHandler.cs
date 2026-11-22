using Microsoft.Extensions.Logging;
using Platform.EventBus;

namespace RiskAssessments.WebApi.IntegrationEvents;

public sealed class ContractorRemovedHandler(RiskCatalogSync sync, ILogger<ContractorRemovedHandler> logger)
{
    public Task HandleAsync(ContractorRemovedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, e.ContractorId, "lastNameEventAt", async (session, token) =>
        {
            var updated = await sync.SetContractorNameAsync(
                session, e.ContractorId, e.ContractorName, active: false, token);
            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "marked contractorActive=false on {DocumentCount} risk assessment documents. Snapshots of tender title were not changed",
                nameof(ContractorRemovedEvent), e.EventId, e.CorrelationId, e.ContractorId, updated);
        }, ct);
}
