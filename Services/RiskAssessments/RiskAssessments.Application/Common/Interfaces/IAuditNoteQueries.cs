using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.Common.Models;

namespace RiskAssessments.Application.Common.Interfaces;

public interface IAuditNoteQueries
{
    Task<PagedResult<AuditNoteDto>> GetByAssessmentAsync(
        string riskAssessmentId,
        int page,
        int pageSize,
        CancellationToken ct);
}
