namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class ProjectClassificationTag
{
    public long ProjectId { get; set; }
    public int TagId { get; set; }
    public DateTime AssignedAt { get; set; }

    public ReconstructionProject Project { get; set; } = null!;
    public ClassificationTag Tag { get; set; } = null!;
}
