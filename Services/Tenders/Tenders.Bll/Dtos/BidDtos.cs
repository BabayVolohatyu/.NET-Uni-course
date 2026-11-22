using System.ComponentModel.DataAnnotations;

namespace Tenders.Bll.Dtos;

/// <summary>Read model for a bid.</summary>
public record BidDto(
    long TenderId,
    long ContractorId,
    string ContractorName,
    string ContractorEDRPOU,
    decimal ProposedPrice,
    decimal PriceReductionPct,
    DateTime SubmittedAt,
    bool IsWinner,
    bool IsDisqualified);

/// <summary>Input for submitting a bid. DataAnnotations validate before BLL is called.</summary>
public class SubmitBidDto
{
    [Range(1, long.MaxValue)]
    public long ContractorId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "ProposedPrice must be positive.")]
    public decimal ProposedPrice { get; set; }

    [Range(0, 99.99)]
    public decimal PriceReductionPct { get; set; }
}

/// <summary>Input for awarding a tender to a specific contractor.</summary>
public class AwardTenderDto
{
    [Range(1, long.MaxValue)]
    public long WinnerContractorId { get; set; }
}
