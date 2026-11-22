namespace Tenders.Domain.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist in the database.
/// Maps to HTTP 404 Not Found.
/// Corresponds to THROW 50001 / 50003 / 50005 / 50006 in stored procedures.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
