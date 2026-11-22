namespace Platform.EventBus;

/// <summary>
/// Publishes an integration event to a topic exchange. Callers must invoke this
/// only after the business change has been committed.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct)
        where T : IntegrationEvent;
}
