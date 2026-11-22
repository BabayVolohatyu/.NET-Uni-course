using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Application.Common.Interfaces;

namespace RiskAssessments.Application.AuditNotes.Commands.CreateAuditNote;

public sealed record CreateAuditNoteCommand(
    string RiskAssessmentId,
    string AuthorId,
    string AuthorName,
    string Text,
    string NoteType,
    bool IsPinned,
    string? AttachmentName,
    string? AttachmentUrl) : ICommand<AuditNoteDto>;
