using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Publishes persistent JSON messages. One <see cref="IConnection"/> for the process;
/// a short-lived <see cref="IChannel"/> per publish (RabbitMQ.Client 7).
/// </summary>
public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public async Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct)
        where T : IntegrationEvent
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await MessagingTopology.DeclareAsync(channel, ct);

        var headers = new Dictionary<string, object?>
        {
            ["x-schema-version"] = @event.SchemaVersion
        };
        if (!string.IsNullOrWhiteSpace(@event.CorrelationId))
            headers["x-correlation-id"] = @event.CorrelationId;

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = headers
        };
        TracePropagation.Inject(props);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonDefaults.Options);
        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, ct);

        logger.LogInformation(
            "Published {EventType} {EventId} CorrelationId={CorrelationId} to {Exchange}/{RoutingKey} SchemaVersion={SchemaVersion}",
            typeof(T).Name, @event.EventId, @event.CorrelationId, exchange, routingKey, @event.SchemaVersion);
    }
}
