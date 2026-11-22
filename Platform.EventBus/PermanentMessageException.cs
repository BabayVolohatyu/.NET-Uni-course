namespace Platform.EventBus;

/// <summary>
/// The message can never be processed (unknown type, incompatible schema, corrupt payload).
/// Consumers nack it without requeue so RabbitMQ routes it to the dead-letter exchange.
/// Transient failures (database down, timeout) must not use this type — those are retried.
/// </summary>
public sealed class PermanentMessageException : Exception
{
    public PermanentMessageException(string message) : base(message)
    {
    }

    public PermanentMessageException(string message, Exception inner) : base(message, inner)
    {
    }
}
