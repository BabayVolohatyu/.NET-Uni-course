using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Domain.Entities;

namespace RiskAssessments.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is missing.");
        if (string.IsNullOrWhiteSpace(value.DatabaseName))
            throw new InvalidOperationException("MongoDb:DatabaseName is missing.");

        Client = new MongoClient(value.ConnectionString);
        Database = Client.GetDatabase(value.DatabaseName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<RiskAssessment> RiskAssessments =>
        Database.GetCollection<RiskAssessment>("riskAssessments");

    public IMongoCollection<AuditNote> AuditNotes =>
        Database.GetCollection<AuditNote>("auditNotes");

    public IMongoCollection<BsonDocument> AnomalyRules =>
        Database.GetCollection<BsonDocument>("anomalyRules");

    public IMongoCollection<BsonDocument> Discussions =>
        Database.GetCollection<BsonDocument>("discussions");
}
