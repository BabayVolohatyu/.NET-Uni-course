namespace RiskAssessments.Domain.Enums;

/// <summary>
/// Auditor-facing severity of a tender assessment.
/// Bands: Low 0–29, Medium 30–54, High 55–79, Critical 80–100.
/// </summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
