namespace RiskAssessments.Infrastructure.Persistence;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";

    public string DatabaseName { get; set; } = "";
}
