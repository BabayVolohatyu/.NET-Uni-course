using System.Text.Json;
using Platform.EventBus;
using RabbitMQ.Client;

namespace Tenders.Api.IntegrationEvents;

/// <summary>
/// Background consumer of <c>tenders.catalog-sync</c>.
/// One queue for this service; risk assessments have their own queue and receive the same events (fan-out).
/// </summary>
public sealed class CatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumerBackgroundService(connection, scopes, logger)
{
    protected override string QueueName => MessagingTopology.TendersCatalogQueue;

    protected override async Task DispatchAsync(
        IServiceProvider services,
        string eventType,
        ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        switch (eventType)
        {
            case nameof(ContractorRenamedEvent):
                var renamed = Deserialize<ContractorRenamedEvent>(body);
                await services.GetRequiredService<ContractorRenamedHandler>().HandleAsync(renamed, ct);
                break;
            case nameof(ContractorCapitalChangedEvent):
                var capital = Deserialize<ContractorCapitalChangedEvent>(body);
                await services.GetRequiredService<ContractorCapitalChangedHandler>().HandleAsync(capital, ct);
                break;
            case nameof(ContractorRegisteredEvent):
                var registered = Deserialize<ContractorRegisteredEvent>(body);
                await services.GetRequiredService<ContractorRegisteredHandler>().HandleAsync(registered, ct);
                break;
            case nameof(ContractorRemovedEvent):
                var removed = Deserialize<ContractorRemovedEvent>(body);
                await services.GetRequiredService<ContractorRemovedHandler>().HandleAsync(removed, ct);
                break;
            default:
                throw new PermanentMessageException($"Tenders does not handle event type '{eventType}'.");
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body) where T : IntegrationEvent =>
        JsonSerializer.Deserialize<T>(body.Span, JsonDefaults.Options)
        ?? throw new JsonException("Empty message");
}
