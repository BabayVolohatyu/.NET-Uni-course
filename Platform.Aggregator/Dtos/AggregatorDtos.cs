using System.Text.Json.Serialization;

namespace Platform.Aggregator.Dtos;

// ── Tender summary (from Tenders.Api) ────────────────────────────────────────
/// <summary>Summarized tender data fetched from Tenders.Api.</summary>
public record TenderSummaryDto(
    long Id,
    string Title,
    string Status,
    decimal ExpectedBudget,
    string ProcuringEntityName,
    string? CategoryCode,
    DateTime? PublishedAt);

// ── Contractor summary (from Catalog.Api) ────────────────────────────────────
/// <summary>Summarized contractor data fetched from Catalog.Api.</summary>
public record ContractorSummaryDto(
    int Id,
    string Name,
    string Edrpou,
    bool IsActive,
    decimal AuthorizedCapital,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Region);

// ── Risk assessment summary (from RiskAssessments.WebApi) ────────────────────
/// <summary>Summarized risk assessment fetched from RiskAssessments.WebApi.</summary>
public record RiskAssessmentSummaryDto(
    string Id,
    string TenderId,
    string TenderTitle,
    string RiskLevel,
    int OverallRiskScore,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? TriggeredFlags);

// ── Full tender aggregate (composite response) ───────────────────────────────
/// <summary>
/// Composite DTO for the "full tender view" aggregation scenario.
/// Combines data from Tenders.Api, Catalog.Api, and RiskAssessments.WebApi.
/// Warnings list records partial failures — if one service is unavailable,
/// the response still returns available data with a warning instead of 500.
/// </summary>
public record FullTenderDto(
    TenderSummaryDto Tender,
    IReadOnlyList<ContractorSummaryDto> Contractors,
    IReadOnlyList<RiskAssessmentSummaryDto> RiskAssessments,
    IReadOnlyList<string> Warnings);

// ── Platform health aggregate ─────────────────────────────────────────────────
/// <summary>Aggregated health status across all microservices.</summary>
public record PlatformHealthDto(
    string OverallStatus,
    IReadOnlyList<ServiceHealthDto> Services,
    IReadOnlyList<string> Warnings);

public record ServiceHealthDto(
    string ServiceName,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Details);
