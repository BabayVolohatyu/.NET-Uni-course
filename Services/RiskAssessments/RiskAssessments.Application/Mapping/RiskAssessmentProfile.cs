using AutoMapper;
using MongoDB.Bson;
using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Application.Mapping;

public sealed class RiskAssessmentProfile : Profile
{
    public RiskAssessmentProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
        CreateMap<TriggeredFlag, TriggeredFlagDto>().ConvertUsing(s => ToFlag(s));
        CreateMap<RiskAssessment, RiskAssessmentDetailsDto>().ConvertUsing(s => ToDetails(s));
        CreateMap<Attachment, AttachmentDto>().ConvertUsing(s => ToAttachment(s));
        CreateMap<AuditNote, AuditNoteDto>().ConvertUsing(s => ToNote(s));
    }

    private static TriggeredFlagDto ToFlag(TriggeredFlag s) => new(
        s.RuleCode,
        s.Severity.ToString(),
        s.Description,
        s.DetectedAt,
        s.AffectedField,
        s.ContractorId,
        s.CompanyAgeDays,
        s.AuthorizedCapital);

    private static AttachmentDto ToAttachment(Attachment s) => new(s.Name, s.Url);

    private static RiskAssessmentDetailsDto ToDetails(RiskAssessment s) => new(
        s.Id.ToString(),
        s.TenderId,
        s.TenderTitle,
        s.ProcuringEntityId,
        s.ProcuringEntityName,
        s.Auditor.UserId,
        s.Auditor.DisplayName,
        s.OverallRiskScore,
        s.RiskLevel.ToString(),
        s.ExpectedBudget == null ? null : s.ExpectedBudget.Amount,
        s.ExpectedBudget == null ? null : s.ExpectedBudget.Currency,
        s.TriggeredFlags.Select(ToFlag).ToArray(),
        s.AuditNoteIds.Select(id => id.ToString()).ToArray(),
        s.Version,
        s.CreatedAt,
        s.UpdatedAt);

    private static AuditNoteDto ToNote(AuditNote s) => new(
        s.Id.ToString(),
        s.RiskAssessmentId.ToString(),
        s.Author.UserId,
        s.Author.DisplayName,
        s.Text,
        s.NoteType.ToString(),
        s.IsPinned,
        s.IsResolved,
        s.DiscussionId.HasValue ? s.DiscussionId.Value.ToString() : null,
        s.Attachments.Select(ToAttachment).ToArray(),
        s.Version,
        s.CreatedAt);
}
