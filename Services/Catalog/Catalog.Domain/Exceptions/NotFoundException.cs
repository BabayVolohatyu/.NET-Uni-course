namespace Catalog.Domain.Exceptions;

/// <summary>
/// Raised when a requested resource (Contractor, WorkCategory, etc.) does not exist.
/// Maps to HTTP 404 Not Found at the API boundary.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}
