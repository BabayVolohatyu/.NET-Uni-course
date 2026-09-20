using MongoDB.Driver;
using SchemaHunter.CommunityAudits.Domain.Documents;

namespace SchemaHunter.CommunityAudits.Infrastructure.Repositories;

/// <summary>
/// Repository for the <c>inspection_audits</c> MongoDB collection.
/// </summary>
public sealed class InspectionAuditRepository
{
    private readonly IMongoCollection<InspectionAudit> _collection;

    public InspectionAuditRepository(IMongoDatabase database)
        => _collection = database.GetCollection<InspectionAudit>("inspection_audits");

    // ─── §6.6 Get by audit code ───────────────────────────────────────────────
    /// <summary>Finds a single audit by its human-readable code.</summary>
    public async Task<InspectionAudit?> GetByAuditCodeAsync(
        string auditCode, CancellationToken ct = default)
        => await _collection
            .Find(a => a.AuditCode == auditCode)
            .FirstOrDefaultAsync(ct);

    // ─── §6.6 Get by project asset code ──────────────────────────────────────
    /// <summary>Returns all audits for a given reconstruction project asset code.</summary>
    public async Task<List<InspectionAudit>> GetByProjectAsync(
        string assetCode, CancellationToken ct = default)
        => await _collection
            .Find(a => a.ProjectReference.AssetCode == assetCode)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <summary>Returns a page of audits filtered by optional status and region.</summary>
    public async Task<List<InspectionAudit>> GetPagedAsync(
        string? status, string? region, int skip, int limit, CancellationToken ct = default)
    {
        var filter = Builders<InspectionAudit>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<InspectionAudit>.Filter.Eq(a => a.Status, status);
        if (!string.IsNullOrWhiteSpace(region))
            filter &= Builders<InspectionAudit>.Filter.Eq("projectReference.region", region);

        return await _collection
            .Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);
    }

    // ─── §6.6 Insert ─────────────────────────────────────────────────────────
    /// <summary>Inserts a new audit document.</summary>
    public async Task InsertAsync(InspectionAudit audit, CancellationToken ct = default)
        => await _collection.InsertOneAsync(audit, cancellationToken: ct);

    // ─── §6.6 Mark finding resolved ──────────────────────────────────────────
    /// <summary>
    /// Sets <c>findings.$.isResolved = true</c> and records <c>resolvedAt</c>
    /// for the matching finding using a positional update.
    /// </summary>
    public async Task MarkFindingResolvedAsync(
        string auditId, string findingId, CancellationToken ct = default)
    {
        var filter = Builders<InspectionAudit>.Filter.And(
            Builders<InspectionAudit>.Filter.Eq(a => a.Id, auditId),
            Builders<InspectionAudit>.Filter.ElemMatch(
                a => a.Findings, f => f.FindingId == findingId));

        var update = Builders<InspectionAudit>.Update
            .Set("findings.$.isResolved", true)
            .Set("findings.$.resolvedAt", DateTime.UtcNow)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    // ─── §7.1 Eventual consistency — project renamed ──────────────────────────
    /// <summary>
    /// Updates all audit documents whose cached project asset code matches
    /// <paramref name="oldAssetCode"/>. Called by the event handler for
    /// <c>ProjectAssetCodeRenamed</c>.
    /// </summary>
    public async Task UpdateProjectReferenceAsync(
        string oldAssetCode, string newAssetCode, string newProjectName, CancellationToken ct = default)
    {
        var filter = Builders<InspectionAudit>.Filter
            .Eq("projectReference.assetCode", oldAssetCode);

        var update = Builders<InspectionAudit>.Update
            .Set("projectReference.assetCode", newAssetCode)
            .Set("projectReference.projectName", newProjectName)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
