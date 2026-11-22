namespace RiskAssessments.Domain.Exceptions;

/// <summary>
/// A procurement-risk invariant was violated inside the domain model.
/// Mapped to HTTP 400 by the WebApi exception handler.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception inner) : base(message, inner)
    {
    }
}
