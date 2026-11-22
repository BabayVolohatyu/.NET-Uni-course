namespace Platform.EventBus;

/// <summary>
/// An auditor verdict was stored. Published only after the MongoDB insert succeeds.
/// Self-contained: consumers do not need to read the assessment document to know what happened.
/// </summary>
public record RiskAssessmentRecordedEvent(
    string AssessmentId,
    long TenderId,
    string TenderTitle,
    string RiskLevel,
    double OverallRiskScore,
    long ProcuringEntityId) : IntegrationEvent;
