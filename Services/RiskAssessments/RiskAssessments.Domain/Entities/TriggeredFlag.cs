using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.Domain.Entities;

/// <summary>
/// One triggered anomaly rule, embedded in <see cref="RiskAssessment"/> (Lab 01 <c>triggeredFlags</c>).
/// Optional numeric fields are omitted when null so each rule keeps its own shape
/// (shell-company flags carry company age and capital; a single-bidder flag does not).
/// </summary>
public class TriggeredFlag : BaseEntity
{
    public string RuleCode { get; private set; } = "";

    public FlagSeverity Severity { get; private set; }

    public string Description { get; private set; } = "";

    public DateTime DetectedAt { get; private set; }

    public string? AffectedField { get; private set; }

    public long? ContractorId { get; private set; }

    /// <summary>
    /// Denormalized contractor name. Event-updated when the registry publishes
    /// <c>ContractorRenamedEvent</c>. Not a snapshot of the bid.
    /// </summary>
    public string? ContractorName { get; private set; }

    /// <summary>False after the registry removes the contractor. Null when the flag never recorded it.</summary>
    public bool? ContractorActive { get; private set; }

    public int? CompanyAgeDays { get; private set; }

    public decimal? AuthorizedCapital { get; private set; }

    private TriggeredFlag()
    {
    }

    public TriggeredFlag(
        string ruleCode,
        FlagSeverity severity,
        string description,
        string? affectedField = null,
        long? contractorId = null,
        int? companyAgeDays = null,
        decimal? authorizedCapital = null,
        string? contractorName = null)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
            throw new DomainException("Rule code is required.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Flag description is required.");

        RuleCode = ruleCode.Trim();
        Severity = severity;
        Description = description.Trim();
        DetectedAt = DateTime.UtcNow;
        AffectedField = string.IsNullOrWhiteSpace(affectedField) ? null : affectedField.Trim();
        ContractorId = contractorId;
        ContractorName = string.IsNullOrWhiteSpace(contractorName) ? null : contractorName.Trim();
        CompanyAgeDays = companyAgeDays;
        AuthorizedCapital = authorizedCapital;
    }
}
