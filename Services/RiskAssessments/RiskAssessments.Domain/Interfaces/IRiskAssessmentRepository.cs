using MongoDB.Bson;
using RiskAssessments.Domain.Entities;

namespace RiskAssessments.Domain.Interfaces;

/// <summary>
/// Write-side contract for risk assessments. Implemented in Infrastructure with the MongoDB driver.
/// </summary>
public interface IRiskAssessmentRepository
{
    Task<RiskAssessment?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task<bool> ExistsAsync(long tenderId, CancellationToken ct = default);

    Task AddAsync(RiskAssessment assessment, CancellationToken ct = default);

    /// <summary>Replaces the document only when the stored version equals the version before <see cref="Common.BaseEntity.BumpVersion"/>.</summary>
    Task UpdateAsync(RiskAssessment assessment, CancellationToken ct = default);

    Task DeleteAsync(ObjectId id, int expectedVersion, CancellationToken ct = default);
}
