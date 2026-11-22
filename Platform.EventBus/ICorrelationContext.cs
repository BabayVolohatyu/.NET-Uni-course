namespace Platform.EventBus;

/// <summary>
/// Business correlation id of the current HTTP request (header X-Correlation-Id).
/// Empty when the call is not inside a request (background work).
/// </summary>
public interface ICorrelationContext
{
    string? Current { get; }
}
