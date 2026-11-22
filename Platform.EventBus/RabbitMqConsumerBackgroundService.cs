using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Long-running consumer. Manual ack: the message stays on the queue until the handler finishes.
/// Transient failures are republished with <c>x-retry-count</c> (nack/requeue cannot change headers).
/// After <see cref="MaxRetries"/>, or on a permanent failure, the message is nacked without requeue
/// and RabbitMQ dead-letters it.
/// </summary>
public abstract class RabbitMqConsumerBackgroundService : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _channelGate = new(1, 1);
    private IChannel? _channel;

    protected RabbitMqConsumerBackgroundService(
        IConnection connection,
        IServiceScopeFactory scopes,
        ILogger logger)
    {
        _connection = connection;
        _scopes = scopes;
        _logger = logger;
    }

    protected abstract string QueueName { get; }

    /// <summary>Deliveries that already carry this retry count are dead-lettered.</summary>
    protected virtual int MaxRetries => 3;

    protected abstract Task DispatchAsync(
        IServiceProvider services,
        string eventType,
        ReadOnlyMemory<byte> body,
        CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await MessagingTopology.DeclareAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;
        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        _logger.LogInformation("Consuming {Queue} with manual ack and prefetch 10", QueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is stopping. StopAsync closes the channel.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
            await _channel.DisposeAsync();
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        await _channelGate.WaitAsync();
        try
        {
            using var activity = TracePropagation.StartConsumerActivity(ea, QueueName);
            var messageId = ea.BasicProperties.MessageId;
            var eventType = ea.BasicProperties.Type;
            var correlationId = ReadHeader(ea, "x-correlation-id");
            activity?.SetTag("messaging.message.conversation_id", correlationId);
            activity?.SetTag("event.type", eventType);

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["EventId"] = messageId,
                ["EventType"] = eventType,
                ["CorrelationId"] = correlationId,
                ["Queue"] = QueueName
            });

            try
            {
                EnsureCompatible(ea);
                await using var scope = _scopes.CreateAsyncScope();
                await DispatchAsync(scope.ServiceProvider, eventType!, ea.Body, CancellationToken.None);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                _logger.LogInformation(
                    "Acked {EventType} {EventId} CorrelationId={CorrelationId} from {Queue}",
                    eventType, messageId, correlationId, QueueName);
            }
            catch (Exception ex) when (ex is JsonException or PermanentMessageException)
            {
                _logger.LogError(
                    ex,
                    "Permanent failure for {EventType} {EventId} CorrelationId={CorrelationId} on {Queue}; nack requeue=false (DLQ)",
                    eventType, messageId, correlationId, QueueName);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                await RetryOrDeadLetterAsync(ea, ex);
            }
        }
        finally
        {
            _channelGate.Release();
        }
    }

    private void EnsureCompatible(BasicDeliverEventArgs ea)
    {
        if (string.IsNullOrWhiteSpace(ea.BasicProperties.Type))
            throw new PermanentMessageException("Message has no event type header.");

        if (ea.Body.Length == 0)
            throw new JsonException("Empty message");

        try
        {
            using var document = JsonDocument.Parse(ea.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Message body is not a JSON object.");

            if (document.RootElement.TryGetProperty("schemaVersion", out var version)
                && version.TryGetInt32(out var schema)
                && schema != 1)
            {
                throw new PermanentMessageException(
                    $"Incompatible schema version {schema} for {ea.BasicProperties.Type}. Expected 1.");
            }
        }
        catch (JsonException)
        {
            throw;
        }
        catch (PermanentMessageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException("Malformed JSON payload.", ex);
        }
    }

    /// <summary>
    /// Republish with an incremented <c>x-retry-count</c>, then ack the original.
    /// <c>BasicNack(requeue: true)</c> would redeliver the same headers, so the counter could not move.
    /// </summary>
    private async Task RetryOrDeadLetterAsync(BasicDeliverEventArgs ea, Exception ex)
    {
        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        var attempt = ReadRetryCount(headers);
        var messageId = ea.BasicProperties.MessageId;

        if (attempt >= MaxRetries)
        {
            _logger.LogError(
                ex,
                "Message {EventId} EventType={EventType} failed after {Attempts} attempts on {Queue}; nack requeue=false (DLQ)",
                messageId, ea.BasicProperties.Type, attempt, QueueName);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        var retryHeaders = new Dictionary<string, object?>(headers)
        {
            ["x-retry-count"] = attempt + 1
        };
        var props = new BasicProperties(ea.BasicProperties)
        {
            Headers = retryHeaders,
            Persistent = true
        };

        await _channel!.BasicPublishAsync(ea.Exchange, ea.RoutingKey, mandatory: false, props, ea.Body);
        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        _logger.LogWarning(
            ex,
            "Message {EventId} EventType={EventType} CorrelationId={CorrelationId} retry {Attempt}/{Max} on {Queue}",
            messageId, ea.BasicProperties.Type, ReadHeader(ea, "x-correlation-id"), attempt + 1, MaxRetries, QueueName);
    }

    private static int ReadRetryCount(IDictionary<string, object?> headers)
    {
        if (!headers.TryGetValue("x-retry-count", out var raw) || raw is null)
            return 0;

        return raw switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => Convert.ToInt32(raw)
        };
    }

    private static string? ReadHeader(BasicDeliverEventArgs ea, string key)
    {
        var headers = ea.BasicProperties.Headers;
        if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
            return null;

        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString()
        };
    }
}
