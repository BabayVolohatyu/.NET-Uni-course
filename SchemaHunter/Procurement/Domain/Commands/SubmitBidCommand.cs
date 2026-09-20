namespace SchemaHunter.Procurement.Domain.Commands;

/// <summary>Input model for <c>usp_SubmitBid</c>.</summary>
public record SubmitBidCommand(
    long     TenderId,
    long     SupplierId,
    decimal  OfferedAmount,
    DateTime SubmittedAt);
