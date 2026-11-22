using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.Common.Models;

namespace RiskAssessments.Application.AuditNotes.Queries.GetAuditNotesByAssessment;

public sealed record GetAuditNotesByAssessmentQuery(
    string RiskAssessmentId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<AuditNoteDto>>;
