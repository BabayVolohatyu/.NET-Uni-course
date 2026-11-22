namespace RiskAssessments.Application.RiskAssessments.Dtos;

public sealed record TriggeredFlagDto(
    string RuleCode,
    string Severity,
    string Description,
    DateTime DetectedAt,
    string? AffectedField,
    long? ContractorId,
    int? CompanyAgeDays,
    decimal? AuthorizedCapital);

public sealed record RiskAssessmentDto(
    string Id,
    long TenderId,
    string TenderTitle,
    long ProcuringEntityId,
    string ProcuringEntityName,
    string AuditorName,
    double OverallRiskScore,
    string RiskLevel,
    int FlagsCount,
    int Version,
    DateTime CreatedAt);

public sealed record RiskAssessmentDetailsDto(
    string Id,
    long TenderId,
    string TenderTitle,
    long ProcuringEntityId,
    string ProcuringEntityName,
    string AuditorId,
    string AuditorName,
    double OverallRiskScore,
    string RiskLevel,
    decimal? ExpectedBudgetAmount,
    string? ExpectedBudgetCurrency,
    IReadOnlyList<TriggeredFlagDto> TriggeredFlags,
    IReadOnlyList<string> AuditNoteIds,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
