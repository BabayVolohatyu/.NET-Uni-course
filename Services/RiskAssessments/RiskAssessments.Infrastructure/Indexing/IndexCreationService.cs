using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.Infrastructure.Indexing;

public interface IIndexCreationService
{
    Task CreateAsync(CancellationToken ct = default);
}

/// <summary>
/// Creates compound, text, and unique indexes at startup. Repeating the call is safe:
/// MongoDB does not duplicate an index with the same key pattern.
/// </summary>
public sealed class IndexCreationService(MongoDbContext context) : IIndexCreationService, IHostedService
{
    public Task StartAsync(CancellationToken ct) => CreateAsync(ct);

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task CreateAsync(CancellationToken ct = default)
    {
        var keys = Builders<RiskAssessment>.IndexKeys;
        await context.RiskAssessments.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<RiskAssessment>(
                keys.Ascending(a => a.TenderId),
                new CreateIndexOptions { Unique = true, Name = "IX_riskAssessments_tenderId" }),
            new CreateIndexModel<RiskAssessment>(
                keys.Ascending(a => a.ProcuringEntityId).Descending(a => a.CreatedAt),
                new CreateIndexOptions { Name = "IX_riskAssessments_entity_date" }),
            new CreateIndexModel<RiskAssessment>(
                keys.Ascending(a => a.RiskLevel).Descending(a => a.OverallRiskScore),
                new CreateIndexOptions { Name = "IX_riskAssessments_riskLevel_score" }),
            new CreateIndexModel<RiskAssessment>(
                keys.Text(a => a.TenderTitle),
                new CreateIndexOptions { Name = "IX_riskAssessments_tenderTitle_text" })
        ], ct);

        var noteKeys = Builders<AuditNote>.IndexKeys;
        await context.AuditNotes.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AuditNote>(
                noteKeys.Ascending(n => n.RiskAssessmentId).Ascending(n => n.CreatedAt),
                new CreateIndexOptions { Name = "IX_auditNotes_assessmentId_date" }),
            new CreateIndexModel<AuditNote>(
                noteKeys.Ascending(n => n.IsResolved).Descending(n => n.IsPinned),
                new CreateIndexOptions { Name = "IX_auditNotes_unresolved_pinned" }),
            new CreateIndexModel<AuditNote>(
                noteKeys.Text(n => n.Text),
                new CreateIndexOptions { Name = "IX_auditNotes_text" })
        ], ct);

        var processed = context.Database.GetCollection<BsonDocument>("processedEvents");
        await processed.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("eventId"),
                new CreateIndexOptions { Unique = true, Name = "UX_processedEvents_eventId" }),
            cancellationToken: ct);

        var syncState = context.Database.GetCollection<BsonDocument>("catalogSyncState");
        await syncState.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("contractorId"),
                new CreateIndexOptions { Unique = true, Name = "UX_catalogSyncState_contractorId" }),
            cancellationToken: ct);

        var ruleKeys = Builders<BsonDocument>.IndexKeys;
        await context.AnomalyRules.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                ruleKeys.Ascending("code"),
                new CreateIndexOptions { Unique = true, Name = "IX_anomalyRules_code" }),
            new CreateIndexModel<BsonDocument>(
                ruleKeys.Ascending("isActive").Ascending("severity"),
                new CreateIndexOptions { Name = "IX_anomalyRules_active_severity" })
        ], ct);
    }
}
