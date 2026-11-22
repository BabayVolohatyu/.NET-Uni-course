namespace RiskAssessments.Domain.Exceptions;

/// <summary>
/// Optimistic-concurrency conflict (stale <c>Version</c> / If-Match) or a duplicate write. HTTP 409.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
