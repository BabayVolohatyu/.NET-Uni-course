namespace Tenders.Dal.Interfaces;

/// <summary>
/// Inserts a processed integration-event id. The primary key is the idempotency token.
/// Must run on the current unit-of-work transaction together with the business change.
/// </summary>
public interface IProcessedEventStore
{
    /// <summary>Returns false when <paramref name="eventId"/> was already stored (SQL 2627/2601).</summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
