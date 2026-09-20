namespace SchemaHunter.Procurement.Domain.DTOs;

/// <summary>
/// Summary row returned as the first result set of
/// <c>procurement.usp_GetRiskReportByProject</c>.
/// </summary>
public record RiskReportSummary(
    string   ProjectCode,
    int      TenderCount,
    decimal  TotalExpectedBudget,
    decimal? AvgRiskScore,
    decimal? MaxRiskScore,
    int      CriticalCount,
    int      HighCount,
    int      MediumCount,
    int      LowCount);
