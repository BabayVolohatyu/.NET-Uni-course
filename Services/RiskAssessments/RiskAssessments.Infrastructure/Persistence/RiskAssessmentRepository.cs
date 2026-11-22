using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Infrastructure.Persistence;

public sealed class RiskAssessmentRepository(MongoDbContext context) : IRiskAssessmentRepository
{
    private readonly IMongoCollection<RiskAssessment> _assessments = context.RiskAssessments;

    public async Task<RiskAssessment?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _assessments.Find(a => a.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long tenderId, CancellationToken ct = default)
        => _assessments.Find(a => a.TenderId == tenderId).AnyAsync(ct);

    public Task AddAsync(RiskAssessment assessment, CancellationToken ct = default)
        => _assessments.InsertOneAsync(assessment, cancellationToken: ct);

    public async Task UpdateAsync(RiskAssessment assessment, CancellationToken ct = default)
    {
        var expectedVersion = assessment.Version;
        assessment.BumpVersion();

        var result = await _assessments.ReplaceOneAsync(
            a => a.Id == assessment.Id && a.Version == expectedVersion,
            assessment,
            cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                $"Risk assessment {assessment.Id} was modified by another auditor.");
        }
    }

    public async Task DeleteAsync(ObjectId id, int expectedVersion, CancellationToken ct = default)
    {
        var result = await _assessments.DeleteOneAsync(
            a => a.Id == id && a.Version == expectedVersion,
            ct);

        if (result.DeletedCount > 0)
            return;

        var exists = await _assessments.Find(a => a.Id == id).AnyAsync(ct);
        if (!exists)
            throw new NotFoundException($"Risk assessment {id} was not found.");

        throw new ConflictException($"Risk assessment {id} was modified by another auditor.");
    }
}
