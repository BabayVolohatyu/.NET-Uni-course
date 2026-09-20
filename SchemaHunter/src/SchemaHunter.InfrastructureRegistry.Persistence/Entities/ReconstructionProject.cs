namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class ReconstructionProject
{
    public long Id { get; set; }

    /// <summary>
    /// Unique asset code used as a cross-context reference key.
    /// Cached by Context 1 (Tender.ProjectCode) and Context 3 (InspectionAudit.projectReference.assetCode).
    /// </summary>
    public string AssetCode { get; set; } = null!;

    public string Name { get; set; } = null!;
    public int AssetCategoryId { get; set; }
    public decimal AllocatedBudget { get; set; }

    /// <summary>Planning | Active | Suspended | Completed</summary>
    public string Status { get; set; } = null!;

    public string Region { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Optimistic concurrency token — maps to SQL Server ROWVERSION.</summary>
    public byte[] RowVersion { get; set; } = null!;

    public AssetCategory AssetCategory { get; set; } = null!;
    public ProjectPassport? Passport { get; set; }
    public ICollection<FundingSource> FundingSources { get; set; } = new List<FundingSource>();
    public ICollection<ProjectClassificationTag> ProjectClassificationTags { get; set; } = new List<ProjectClassificationTag>();
}
