namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class AssetCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ReconstructionProject> Projects { get; set; } = new List<ReconstructionProject>();
}
