using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Tenders.Dal.Interfaces;

namespace Tenders.Api.IntegrationEvents;

public sealed class ContractorRemovedHandler(
    LocalContractorSync sync,
    ILogger<ContractorRemovedHandler> logger)
{
    public Task HandleAsync(ContractorRemovedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, async (uow, token) =>
        {
            var removed = await uow.Contractors.TryRemoveUnreferencedAsync(e.ContractorId, token);
            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "local directory rows deleted={Deleted}. Bid snapshot columns were not rewritten",
                nameof(ContractorRemovedEvent), e.EventId, e.CorrelationId, e.ContractorId, removed);
        }, ct);
}
