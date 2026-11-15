namespace Tenders.Domain.Exceptions;

/// <summary>
/// Thrown when a business-rule constraint is violated (invalid state transition,
/// insufficient resources, duplicate key, etc.).
/// Maps to HTTP 409 Conflict.
/// Corresponds to THROW 50002 / 50004 / 50007 / 50008 in stored procedures,
/// and to SqlException numbers 2601 / 2627 (UNIQUE violation).
/// </summary>
public sealed class BusinessConflictException : Exception
{
    public BusinessConflictException(string message) : base(message) { }
    public BusinessConflictException(string message, Exception innerException) : base(message, innerException) { }
}
