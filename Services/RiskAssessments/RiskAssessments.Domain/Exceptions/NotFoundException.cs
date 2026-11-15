namespace RiskAssessments.Domain.Exceptions;

/// <summary>The requested assessment, note, or tender reference does not exist. HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}
