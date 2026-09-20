namespace SchemaHunter.Procurement.Domain.DTOs;

/// <summary>
/// One row returned by <c>procurement.usp_GetTendersPaged</c>.
/// Used for the paged tender list endpoint.
/// </summary>
public record TenderPageItem(
    long     TenderId,
    string   ProzorroId,
    string?  ProjectCode,
    string   Title,
    string   ProcuringEntity,
    decimal  ExpectedValue,
    string   Currency,
    string   Status,
    DateTime PublishedAt,
    DateTime? DeadlineAt,
    string?  RiskSeverity,
    decimal? RiskScore);
