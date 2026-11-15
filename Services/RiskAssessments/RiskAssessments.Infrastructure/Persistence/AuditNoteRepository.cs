using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Infrastructure.Persistence;

public sealed class AuditNoteRepository(MongoDbContext context) : IAuditNoteRepository
{
    private readonly IMongoCollection<AuditNote> _notes = context.AuditNotes;

    public async Task<AuditNote?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _notes.Find(n => n.Id == id).FirstOrDefaultAsync(ct);

    public Task AddAsync(AuditNote note, CancellationToken ct = default)
        => _notes.InsertOneAsync(note, cancellationToken: ct);
}
