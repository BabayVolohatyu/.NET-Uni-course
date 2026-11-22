using Microsoft.Extensions.Logging;
using Platform.EventBus;
using Tenders.Dal.Interfaces;

namespace Tenders.Api.IntegrationEvents;

/// <summary>
/// Runs a catalog-event handler inside one SQL transaction:
/// insert <c>ProcessedEvents.EventId</c>, then the directory change, then commit.
/// A duplicate primary key rolls the transaction back and skips the change.
/// </summary>
public sealed class LocalContractorSync(IUnitOfWork uow, ILogger<LocalContractorSync> logger)
{
    public async Task ExecuteAsync(
        IntegrationEvent integrationEvent,
        Func<IUnitOfWork, CancellationToken, Task> mutate,
        CancellationToken ct)
    {
        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(integrationEvent.EventId, integrationEvent.GetType().Name, ct))
            {
                logger.LogInformation(
                    "Duplicate event {EventId} EventType={EventType} CorrelationId={CorrelationId} skipped",
                    integrationEvent.EventId, integrationEvent.GetType().Name, integrationEvent.CorrelationId);
                await uow.RollbackAsync(ct);
                return;
            }

            await mutate(uow, ct);
            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
