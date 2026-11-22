using Microsoft.Extensions.Logging;
using Platform.EventBus;

namespace RiskAssessments.WebApi.IntegrationEvents;

/// <summary>
/// Writes the new contractor name onto every risk-assessment document that embeds this contractor.
/// <c>tenderTitle</c> and <c>procuringEntityName</c> stay snapshots.
/// <c>triggeredFlags.authorizedCapital</c> stays the capital observed when the flag was raised.
/// </summary>
public sealed class ContractorRenamedHandler(RiskCatalogSync sync, ILogger<ContractorRenamedHandler> logger)
{
    public Task HandleAsync(ContractorRenamedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, e.ContractorId, "lastNameEventAt", async (session, token) =>
        {
            var updated = await sync.SetContractorNameAsync(session, e.ContractorId, e.NewName, active: true, token);
            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "set contractorName to {ContractorName} on {DocumentCount} risk assessment documents. " +
                "tenderTitle, procuringEntityName and triggeredFlags.authorizedCapital were not changed",
                nameof(ContractorRenamedEvent), e.EventId, e.CorrelationId, e.ContractorId, e.NewName, updated);
        }, ct);
}
