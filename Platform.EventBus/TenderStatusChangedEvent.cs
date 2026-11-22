namespace Platform.EventBus;

/// <summary>
/// Tender lifecycle status changed. Domain analogue of OrderStatusChangedEvent.
/// Published only after <c>usp_ChangeTenderStatus</c> has committed.
/// </summary>
public record TenderStatusChangedEvent(
    long TenderId,
    string TenderTitle,
    string OldStatus,
    string NewStatus,
    string? Reason) : IntegrationEvent;
