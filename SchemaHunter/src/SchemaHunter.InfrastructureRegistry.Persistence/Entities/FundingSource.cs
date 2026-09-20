namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class FundingSource
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string FunderName { get; set; } = null!;

    /// <summary>GrantEU | NationalBudget | Municipal | USAID | WorldBank | Other</summary>
    public string FundType { get; set; } = null!;

    public decimal AllocatedAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string? GrantAgreementNumber { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public ReconstructionProject Project { get; set; } = null!;
}
