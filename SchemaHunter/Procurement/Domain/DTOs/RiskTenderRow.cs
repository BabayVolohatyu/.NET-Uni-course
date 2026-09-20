namespace SchemaHunter.Procurement.Domain.DTOs;

/// <summary>
/// One tender detail row returned as the second result set of
/// <c>procurement.usp_GetRiskReportByProject</c>.
/// </summary>
public record RiskTenderRow(
    long     TenderId,
    string   ProzorroId,
    string   Title,
    decimal  ExpectedValue,
    string   Status,
    string   SeverityLevel,
    decimal  OverallScore,
    DateTime AssessedAt);
