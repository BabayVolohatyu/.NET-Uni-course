using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Domain.Entities;

namespace RiskAssessments.Infrastructure.Persistence;

public sealed class AuditNoteQueries(MongoDbContext context) : IAuditNoteQueries
{
    private readonly IMongoCollection<AuditNote> _notes = context.AuditNotes;

    public async Task<PagedResult<AuditNoteDto>> GetByAssessmentAsync(
        string riskAssessmentId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var assessmentId = ObjectId.Parse(riskAssessmentId);
        var filter = Builders<AuditNote>.Filter.Eq(n => n.RiskAssessmentId, assessmentId);
        var total = await _notes.CountDocumentsAsync(filter, cancellationToken: ct);

        var docs = await _notes.Aggregate()
            .Match(filter)
            .Sort(Builders<AuditNote>.Sort.Ascending(n => n.CreatedAt))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(Builders<AuditNote>.Projection
                .Include(n => n.RiskAssessmentId)
                .Include(n => n.Author)
                .Include(n => n.AuthorId)
                .Include(n => n.AuthorName)
                .Include(n => n.Text)
                .Include(n => n.NoteType)
                .Include(n => n.IsPinned)
                .Include(n => n.IsResolved)
                .Include(n => n.DiscussionId)
                .Include("smallAttachments")
                .Include(n => n.Version)
                .Include(n => n.CreatedAt))
            .As<AuditNoteReadModel>()
            .ToListAsync(ct);

        return new PagedResult<AuditNoteDto>(docs.Select(ToDto).ToArray(), page, pageSize, total);
    }

    private static AuditNoteDto ToDto(AuditNoteReadModel doc) => new(
        doc.Id.ToString(),
        doc.RiskAssessmentId.ToString(),
        doc.Author?.UserId ?? doc.AuthorId,
        doc.Author?.DisplayName ?? doc.AuthorName,
        doc.Text,
        doc.NoteType,
        doc.IsPinned,
        doc.IsResolved,
        doc.DiscussionId?.ToString(),
        doc.Attachments.Select(a => new AttachmentDto(a.Name, a.Url)).ToArray(),
        doc.Version,
        doc.CreatedAt);
}
