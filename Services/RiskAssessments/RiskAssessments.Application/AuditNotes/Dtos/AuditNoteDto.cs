namespace RiskAssessments.Application.AuditNotes.Dtos;

public sealed record AttachmentDto(string Name, string Url);

public sealed record AuditNoteDto(
    string Id,
    string RiskAssessmentId,
    string AuthorId,
    string AuthorName,
    string Text,
    string NoteType,
    bool IsPinned,
    bool IsResolved,
    string? DiscussionId,
    IReadOnlyList<AttachmentDto> Attachments,
    int Version,
    DateTime CreatedAt);
