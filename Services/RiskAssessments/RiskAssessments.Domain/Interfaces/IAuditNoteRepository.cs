using MongoDB.Bson;
using RiskAssessments.Domain.Entities;

namespace RiskAssessments.Domain.Interfaces;

public interface IAuditNoteRepository
{
    Task<AuditNote?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task AddAsync(AuditNote note, CancellationToken ct = default);
}
