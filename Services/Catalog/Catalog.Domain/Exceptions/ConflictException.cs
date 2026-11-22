namespace Catalog.Domain.Exceptions;

/// <summary>
/// Raised when a business-level conflict is detected that requires external data
/// (e.g. EDRPOU uniqueness checked via the database before INSERT).
/// Maps to HTTP 409 Conflict at the API boundary.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
