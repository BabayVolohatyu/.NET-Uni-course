using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;

namespace RiskAssessments.Infrastructure.Persistence;

/// <summary>
/// Read side. Filters, skip/limit, and field projection run inside MongoDB.
/// </summary>
public sealed class RiskAssessmentQueries(MongoDbContext context) : IRiskAssessmentQueries
{
    private readonly IMongoCollection<RiskAssessment> _assessments = context.RiskAssessments;

    private static readonly ProjectionDefinition<RiskAssessment> Projection =
        Builders<RiskAssessment>.Projection
            .Include(a => a.TenderId)
            .Include(a => a.TenderTitle)
            .Include(a => a.ProcuringEntityId)
            .Include(a => a.ProcuringEntityName)
            .Include(a => a.Auditor)
            .Include(a => a.AssessedBy)
            .Include(a => a.OverallRiskScore)
            .Include(a => a.RiskLevel)
            .Include(a => a.ExpectedBudget)
            .Include("triggeredFlags")
            .Include("auditNoteIds")
            .Include(a => a.Version)
            .Include(a => a.CreatedAt)
            .Include(a => a.UpdatedAt);

    public async Task<RiskAssessmentDetailsDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var doc = await _assessments.Aggregate()
            .Match(a => a.Id == objectId)
            .Project(Projection)
            .As<RiskAssessmentReadModel>()
            .FirstOrDefaultAsync(ct);

        return doc is null ? null : ToDetails(doc);
    }

    public async Task<PagedResult<RiskAssessmentDto>> GetListAsync(
        long? procuringEntityId,
        string? riskLevel,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var filter = BuildListFilter(procuringEntityId, riskLevel);
        var sort = string.IsNullOrWhiteSpace(riskLevel)
            ? Builders<RiskAssessment>.Sort.Descending(a => a.CreatedAt)
            : Builders<RiskAssessment>.Sort.Descending(a => a.OverallRiskScore);

        var total = await _assessments.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await _assessments.Aggregate()
            .Match(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(Projection)
            .As<RiskAssessmentReadModel>()
            .ToListAsync(ct);

        return new PagedResult<RiskAssessmentDto>(
            docs.Select(ToListItem).ToArray(),
            page,
            pageSize,
            total);
    }

    public async Task<CursorPage<RiskAssessmentDto>> GetCursorPageAsync(
        long? procuringEntityId,
        string? afterId,
        int pageSize,
        CancellationToken ct)
    {
        var filter = Builders<RiskAssessment>.Filter.Empty;
        if (procuringEntityId is long entityId)
            filter &= Builders<RiskAssessment>.Filter.Eq(a => a.ProcuringEntityId, entityId);
        if (!string.IsNullOrWhiteSpace(afterId))
            filter &= Builders<RiskAssessment>.Filter.Lt(a => a.Id, ObjectId.Parse(afterId));

        var docs = await _assessments.Find(filter)
            .SortByDescending(a => a.Id)
            .Limit(pageSize + 1)
            .Project(Projection)
            .As<RiskAssessmentReadModel>()
            .ToListAsync(ct);

        var hasMore = docs.Count > pageSize;
        var page = hasMore ? docs.Take(pageSize).ToArray() : docs.ToArray();
        var next = hasMore ? page[^1].Id.ToString() : null;
        return new CursorPage<RiskAssessmentDto>(page.Select(ToListItem).ToArray(), next);
    }

    public async Task<PagedResult<RiskAssessmentDto>> SearchAsync(
        string text,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var filter = Builders<RiskAssessment>.Filter.Text(text);
        var total = await _assessments.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await _assessments.Aggregate()
            .Match(filter)
            .Sort(Builders<RiskAssessment>.Sort.Descending(a => a.CreatedAt))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(Projection)
            .As<RiskAssessmentReadModel>()
            .ToListAsync(ct);

        return new PagedResult<RiskAssessmentDto>(
            docs.Select(ToListItem).ToArray(),
            page,
            pageSize,
            total);
    }

    private static FilterDefinition<RiskAssessment> BuildListFilter(long? procuringEntityId, string? riskLevel)
    {
        var filter = Builders<RiskAssessment>.Filter.Empty;
        if (procuringEntityId is long entityId)
            filter &= Builders<RiskAssessment>.Filter.Eq(a => a.ProcuringEntityId, entityId);
        if (!string.IsNullOrWhiteSpace(riskLevel)
            && Enum.TryParse<RiskLevel>(riskLevel, ignoreCase: true, out var level))
            filter &= Builders<RiskAssessment>.Filter.Eq(a => a.RiskLevel, level);
        return filter;
    }

    private static RiskAssessmentDto ToListItem(RiskAssessmentReadModel doc) => new(
        doc.Id.ToString(),
        doc.TenderId,
        doc.TenderTitle,
        doc.ProcuringEntityId,
        doc.ProcuringEntityName,
        doc.Auditor?.DisplayName ?? doc.AssessedBy ?? "",
        doc.OverallRiskScore,
        doc.RiskLevel,
        doc.TriggeredFlags.Count,
        doc.Version,
        doc.CreatedAt);

    private static RiskAssessmentDetailsDto ToDetails(RiskAssessmentReadModel doc) => new(
        doc.Id.ToString(),
        doc.TenderId,
        doc.TenderTitle,
        doc.ProcuringEntityId,
        doc.ProcuringEntityName,
        doc.Auditor?.UserId ?? "",
        doc.Auditor?.DisplayName ?? doc.AssessedBy ?? "",
        doc.OverallRiskScore,
        doc.RiskLevel,
        doc.ExpectedBudget?.Amount,
        doc.ExpectedBudget?.Currency,
        doc.TriggeredFlags.Select(f => new TriggeredFlagDto(
            f.RuleCode,
            f.Severity,
            f.Description,
            f.DetectedAt,
            f.AffectedField,
            f.ContractorId,
            f.CompanyAgeDays,
            f.AuthorizedCapital)).ToArray(),
        doc.AuditNoteIds.Select(id => id.ToString()).ToArray(),
        doc.Version,
        doc.CreatedAt,
        doc.UpdatedAt);
}
