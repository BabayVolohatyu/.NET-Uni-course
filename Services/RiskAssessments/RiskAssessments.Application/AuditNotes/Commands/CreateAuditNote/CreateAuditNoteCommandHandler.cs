using AutoMapper;
using MediatR;
using MongoDB.Bson;
using RiskAssessments.Application.AuditNotes.Dtos;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.Interfaces;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Application.AuditNotes.Commands.CreateAuditNote;

public sealed class CreateAuditNoteCommandHandler(
    IAuditNoteRepository notes,
    IRiskAssessmentRepository assessments,
    IMapper mapper)
    : IRequestHandler<CreateAuditNoteCommand, AuditNoteDto>
{
    public async Task<AuditNoteDto> Handle(CreateAuditNoteCommand cmd, CancellationToken ct)
    {
        var assessmentId = ObjectId.Parse(cmd.RiskAssessmentId);
        var assessment = await assessments.GetByIdAsync(assessmentId, ct)
            ?? throw new NotFoundException($"Risk assessment {cmd.RiskAssessmentId} was not found.");

        var note = new AuditNote(
            assessmentId,
            new Auditor(cmd.AuthorId, cmd.AuthorName),
            cmd.Text,
            Enum.Parse<NoteType>(cmd.NoteType, true),
            cmd.IsPinned);

        if (!string.IsNullOrWhiteSpace(cmd.AttachmentName) && !string.IsNullOrWhiteSpace(cmd.AttachmentUrl))
            note.AddAttachment(new Attachment(cmd.AttachmentName, cmd.AttachmentUrl));

        await notes.AddAsync(note, ct);

        assessment.LinkAuditNote(note.Id);
        await assessments.UpdateAsync(assessment, ct);

        return mapper.Map<AuditNoteDto>(note);
    }
}
