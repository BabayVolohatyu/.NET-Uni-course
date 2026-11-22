using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Tenders.Dal.Interfaces;

namespace Tenders.Api.IntegrationEvents;

public sealed class ContractorRegisteredHandler(
    LocalContractorSync sync,
    ILogger<ContractorRegisteredHandler> logger)
{
    public Task HandleAsync(ContractorRegisteredEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, async (uow, token) =>
        {
            var applied = await uow.Contractors.ApplyCatalogSnapshotAsync(
                e.ContractorId, e.ContractorName, e.Edrpou, e.AuthorizedCapital, e.OccurredAt, token);

            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} inserted or refreshed local contractor {ContractorId} {ContractorName}. RowsApplied={RowsApplied}",
                nameof(ContractorRegisteredEvent), e.EventId, e.CorrelationId, e.ContractorId, e.ContractorName, applied);
        }, ct);
}
