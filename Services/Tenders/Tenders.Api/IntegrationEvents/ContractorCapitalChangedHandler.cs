using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Tenders.Dal.Interfaces;

namespace Tenders.Api.IntegrationEvents;

/// <summary>
/// Updates authorized capital on the local contractor directory.
/// Does not change <c>Bids.ProposedPrice</c> — that price is fixed when the bid is placed
/// (same rule as OrderItems.UnitPrice in the course handout).
/// </summary>
public sealed class ContractorCapitalChangedHandler(
    LocalContractorSync sync,
    ILogger<ContractorCapitalChangedHandler> logger)
{
    public Task HandleAsync(ContractorCapitalChangedEvent e, CancellationToken ct) =>
        sync.ExecuteAsync(e, async (uow, token) =>
        {
            var applied = await uow.Contractors.ApplyCatalogSnapshotAsync(
                e.ContractorId, e.ContractorName, e.Edrpou, e.NewAuthorizedCapital, e.OccurredAt, token);

            logger.LogInformation(
                "Event {EventType} {EventId} CorrelationId={CorrelationId} ContractorId={ContractorId} " +
                "set local Contractors.AuthorizedCapital from {OldCapital} to {NewCapital}. RowsApplied={RowsApplied}. " +
                "Bids.ProposedPrice was not updated",
                nameof(ContractorCapitalChangedEvent), e.EventId, e.CorrelationId, e.ContractorId,
                e.OldAuthorizedCapital, e.NewAuthorizedCapital, applied);
        }, ct);
}
