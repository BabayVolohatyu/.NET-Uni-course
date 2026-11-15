namespace RiskAssessments.Application.Common.Caching;

/// <summary>
/// Key prefix reserved for the read-through cache added in Lab 06.
/// Lab 04 does not register a caching behavior: an auditor must see the score
/// that was just written, not a stale entry.
/// </summary>
public static class RiskAssessmentCache
{
    public const string KeyPrefix = "risk-assessment:";
}
