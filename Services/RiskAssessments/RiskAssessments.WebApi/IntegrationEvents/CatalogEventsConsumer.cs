using System.Text.Json;
using Platform.EventBus;
using RabbitMQ.Client;

namespace RiskAssessments.WebApi.IntegrationEvents;

/// <summary>
/// Background consumer of <c>risk-assessments.catalog-sync</c> (the reviews-service queue in the handout).
/// </summary>
public sealed class CatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumerBackgroundService(connection, scopes, logger)
{
    protected override string QueueName => MessagingTopology.RiskCatalogQueue;

    protected override async Task DispatchAsync(
        IServiceProvider services,
        string eventType,
        ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        switch (eventType)
        {
            case nameof(ContractorRenamedEvent):
                await services.GetRequiredService<ContractorRenamedHandler>()
                    .HandleAsync(Deserialize<ContractorRenamedEvent>(body), ct);
                break;
            case nameof(ContractorCapitalChangedEvent):
                await services.GetRequiredService<ContractorCapitalChangedHandler>()
                    .HandleAsync(Deserialize<ContractorCapitalChangedEvent>(body), ct);
                break;
            case nameof(ContractorRegisteredEvent):
                await services.GetRequiredService<ContractorRegisteredHandler>()
                    .HandleAsync(Deserialize<ContractorRegisteredEvent>(body), ct);
                break;
            case nameof(ContractorRemovedEvent):
                await services.GetRequiredService<ContractorRemovedHandler>()
                    .HandleAsync(Deserialize<ContractorRemovedEvent>(body), ct);
                break;
            default:
                throw new PermanentMessageException($"Risk assessments does not handle event type '{eventType}'.");
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body) where T : IntegrationEvent =>
        JsonSerializer.Deserialize<T>(body.Span, JsonDefaults.Options)
        ?? throw new JsonException("Empty message");
}
