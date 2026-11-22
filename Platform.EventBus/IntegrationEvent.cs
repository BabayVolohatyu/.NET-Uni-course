namespace Platform.EventBus;

/// <summary>
/// Immutable, self-contained integration event.
/// <see cref="EventId"/> makes consumers idempotent under at-least-once delivery.
/// <see cref="CorrelationId"/> is the business trace from Lab 05 (X-Correlation-Id).
/// <see cref="SchemaVersion"/> only grows by adding fields — existing names are never renamed or removed.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }

    public int SchemaVersion { get; init; } = 1;
}
