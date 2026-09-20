namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class ClassificationTag
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public bool IsDeleted { get; set; }

    public ICollection<ProjectClassificationTag> ProjectClassificationTags { get; set; } = new List<ProjectClassificationTag>();
}
