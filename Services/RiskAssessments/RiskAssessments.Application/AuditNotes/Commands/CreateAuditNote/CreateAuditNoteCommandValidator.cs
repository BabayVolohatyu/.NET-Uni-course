using FluentValidation;
using MongoDB.Bson;
using RiskAssessments.Domain.Enums;

namespace RiskAssessments.Application.AuditNotes.Commands.CreateAuditNote;

public sealed class CreateAuditNoteCommandValidator : AbstractValidator<CreateAuditNoteCommand>
{
    public CreateAuditNoteCommandValidator()
    {
        RuleFor(x => x.RiskAssessmentId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("RiskAssessmentId must be a valid ObjectId.");
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(80);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.NoteType)
            .Must(value => Enum.TryParse<NoteType>(value, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("NoteType must be Observation, Request, Decision, or Escalation.");
        RuleFor(x => x.AttachmentName)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.AttachmentUrl))
            .WithMessage("Attachment name is required when a url is set.");
        RuleFor(x => x.AttachmentUrl)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.AttachmentName))
            .WithMessage("Attachment url is required when a name is set.");
    }
}
