using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.Domain.ValueObjects;

/// <summary>
/// Small file link embedded on an audit note (<c>smallAttachments</c> in Lab 01).
/// Large discussion threads are referenced, not embedded.
/// </summary>
public sealed class Attachment : ValueObject
{
    public string Name { get; }

    public string Url { get; }

    public Attachment(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Attachment name is required.");

        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Attachment url is required.");

        Name = name.Trim();
        Url = url.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Url;
    }
}
