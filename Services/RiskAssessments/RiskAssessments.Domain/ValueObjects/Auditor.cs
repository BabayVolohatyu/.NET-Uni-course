using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.Domain.ValueObjects;

/// <summary>
/// Who recorded an assessment or an audit note. Stored as an embedded document
/// (the Lab 01 <c>author</c> shape: user id + display name).
/// </summary>
public sealed class Auditor : ValueObject
{
    public string UserId { get; }

    public string DisplayName { get; }

    public Auditor(string userId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Auditor user id is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Auditor display name is required.");

        UserId = userId.Trim();
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }
}
