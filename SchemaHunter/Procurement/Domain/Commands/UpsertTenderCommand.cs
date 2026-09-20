namespace SchemaHunter.Procurement.Domain.Commands;

/// <summary>Input model for <c>usp_UpsertTender</c>.</summary>
public record UpsertTenderCommand(
    string   ProzorroId,
    string?  ProjectCode,
    string   Title,
    string   ProcuringEntity,
    decimal  ExpectedValue,
    string   Currency,
    string   Status,
    DateTime PublishedAt,
    DateTime? DeadlineAt);
