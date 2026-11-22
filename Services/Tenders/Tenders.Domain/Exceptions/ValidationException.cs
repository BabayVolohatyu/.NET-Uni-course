namespace Tenders.Domain.Exceptions;

/// <summary>
/// Thrown when input data fails domain-level business validation
/// (e.g., budget must be positive, deadline must be in the future).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException) : base(message, innerException) { }
}
