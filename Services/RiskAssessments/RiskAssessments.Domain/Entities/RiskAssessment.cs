using MongoDB.Bson;
using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Domain.Entities;

/// <summary>
/// Per-tender risk verdict stored in the <c>riskAssessments</c> collection.
/// <para>
/// <c>tenderTitle</c> and <c>procuringEntityName</c> are snapshots from the Bidding context (P1).
/// There is no foreign key across databases. <see cref="TriggeredFlag"/> values are embedded;
/// audit notes are referenced by id because a thread can grow large.
/// </para>
/// </summary>
public class RiskAssessment : BaseEntity
{
    private List<TriggeredFlag> _triggeredFlags = [];

    private List<ObjectId> _auditNoteIds = [];

    public long TenderId { get; private set; }

    /// <summary>Snapshot of the tender title at evaluation time. Owner: Bidding (P1).</summary>
    public string TenderTitle { get; private set; } = "";

    public long ProcuringEntityId { get; private set; }

    /// <summary>Snapshot of the procuring-entity name. Owner: Bidding (P1).</summary>
    public string ProcuringEntityName { get; private set; } = "";

    public double OverallRiskScore { get; private set; }

    public RiskLevel RiskLevel { get; private set; }

    public Auditor Auditor { get; private set; } = null!;

    /// <summary>Flat copy of <see cref="Auditor.DisplayName"/> kept for the Lab 01 document shape.</summary>
    public string AssessedBy { get; private set; } = "";

    public DateTime AssessedAt { get; private set; }

    public Money? ExpectedBudget { get; private set; }

    public IReadOnlyCollection<TriggeredFlag> TriggeredFlags => _triggeredFlags.AsReadOnly();

    public IReadOnlyCollection<ObjectId> AuditNoteIds => _auditNoteIds.AsReadOnly();

    private RiskAssessment()
    {
    }

    public RiskAssessment(
        long tenderId,
        string tenderTitle,
        long procuringEntityId,
        string procuringEntityName,
        Auditor auditor,
        double overallRiskScore,
        RiskLevel riskLevel,
        Money? expectedBudget = null)
    {
        if (tenderId <= 0)
            throw new DomainException("TenderId must be a positive id from the Bidding context.");

        if (string.IsNullOrWhiteSpace(tenderTitle))
            throw new DomainException("Tender title snapshot is required.");

        if (procuringEntityId <= 0)
            throw new DomainException("ProcuringEntityId must be a positive id from the Bidding context.");

        if (string.IsNullOrWhiteSpace(procuringEntityName))
            throw new DomainException("Procuring entity name snapshot is required.");

        ArgumentNullException.ThrowIfNull(auditor);
        EnsureScore(overallRiskScore, riskLevel);

        TenderId = tenderId;
        TenderTitle = tenderTitle.Trim();
        ProcuringEntityId = procuringEntityId;
        ProcuringEntityName = procuringEntityName.Trim();
        Auditor = auditor;
        AssessedBy = auditor.DisplayName;
        AssessedAt = DateTime.UtcNow;
        OverallRiskScore = overallRiskScore;
        RiskLevel = riskLevel;
        ExpectedBudget = expectedBudget;
    }

    public void AddFlag(TriggeredFlag flag)
    {
        ArgumentNullException.ThrowIfNull(flag);

        if (_triggeredFlags.Any(existing =>
                string.Equals(existing.RuleCode, flag.RuleCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Rule {flag.RuleCode} is already recorded on this assessment.");
        }

        _triggeredFlags.Add(flag);
        Touch();
    }

    public void Revise(double overallRiskScore, RiskLevel riskLevel)
    {
        EnsureScore(overallRiskScore, riskLevel);
        OverallRiskScore = overallRiskScore;
        RiskLevel = riskLevel;
        Touch();
    }

    public void LinkAuditNote(ObjectId noteId)
    {
        if (noteId == ObjectId.Empty)
            throw new DomainException("Audit note id is required.");

        if (_auditNoteIds.Contains(noteId))
            return;

        _auditNoteIds.Add(noteId);
        Touch();
    }

    public static void EnsureScore(double score, RiskLevel level)
    {
        if (score is < 0 or > 100)
            throw new DomainException("Risk score must be between 0 and 100.");

        if (!LevelMatchesScore(score, level))
        {
            throw new DomainException(
                $"Risk level {level} does not match score {score}. " +
                "Bands: Low 0–29, Medium 30–54, High 55–79, Critical 80–100.");
        }
    }

    public static bool LevelMatchesScore(double score, RiskLevel level) => level switch
    {
        RiskLevel.Low => score < 30,
        RiskLevel.Medium => score >= 30 && score < 55,
        RiskLevel.High => score >= 55 && score < 80,
        RiskLevel.Critical => score >= 80 && score <= 100,
        _ => false
    };
}
