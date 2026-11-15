using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;

namespace RiskAssessments.Application.Common.Interfaces;

/// <summary>
/// Read model for risk assessments. The handler calls this interface;
/// Infrastructure runs the MongoDB aggregation (match / skip / limit / project).
/// This is the Lab 04 <c>IReviewQueries</c> port, named for the risk-assessment context.
/// </summary>
public interface IRiskAssessmentQueries
{
    Task<RiskAssessmentDetailsDto?> GetByIdAsync(string id, CancellationToken ct);

    Task<PagedResult<RiskAssessmentDto>> GetListAsync(
        long? procuringEntityId,
        string? riskLevel,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<CursorPage<RiskAssessmentDto>> GetCursorPageAsync(
        long? procuringEntityId,
        string? afterId,
        int pageSize,
        CancellationToken ct);

    Task<PagedResult<RiskAssessmentDto>> SearchAsync(
        string text,
        int page,
        int pageSize,
        CancellationToken ct);
}
