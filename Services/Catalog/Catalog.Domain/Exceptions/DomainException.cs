namespace Catalog.Domain.Exceptions;

/// <summary>
/// Raised when a business-rule invariant is violated within a domain entity
/// (e.g. authorized capital ≤ 0, trying to deactivate an already-inactive contractor).
/// Maps to HTTP 409 Conflict at the API boundary.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
