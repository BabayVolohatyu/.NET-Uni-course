using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Tenders.Dal.Interfaces;

namespace Tenders.Api.IntegrationEvents;

/// <summary>
/// Applies a contractor rename to the local directory only.
/// <c>Bids.ContractorName</c> and <c>Bids.ContractorEDRPOU</c> stay as submitted.
/// </summary>
public sealed class ContractorRenamedHandler(LocalContractorSync sync, ILogger<ContractorRenamedHandler> logger)
{
    public Task HandleAsync(ContractorRenamedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, async (uow, token) =>
        {
            var applied = await uow.Contractors.ApplyCatalogSnapshotAsync(
                e.ContractorId, e.NewName, e.Edrpou, authorizedCapital: null, e.OccurredAt, token);

            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "set local Contractors.Name to {ContractorName}. RowsApplied={RowsApplied}. " +
                "Bids.ContractorName, Bids.ContractorEDRPOU and Bids.ProposedPrice were not updated",
                nameof(ContractorRenamedEvent), e.EventId, e.CorrelationId, e.ContractorId, e.NewName, applied);
        }, ct);
}
