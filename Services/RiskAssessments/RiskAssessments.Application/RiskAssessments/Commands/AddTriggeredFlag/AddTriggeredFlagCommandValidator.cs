using FluentValidation;
using MongoDB.Bson;
using RiskAssessments.Domain.Enums;

namespace RiskAssessments.Application.RiskAssessments.Commands.AddTriggeredFlag;

public sealed class AddTriggeredFlagCommandValidator : AbstractValidator<AddTriggeredFlagCommand>
{
    public AddTriggeredFlagCommandValidator()
    {
        RuleFor(x => x.AssessmentId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("AssessmentId must be a valid ObjectId.");
        RuleFor(x => x.RuleCode).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Severity)
            .Must(value => Enum.TryParse<FlagSeverity>(value, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Severity must be Info, Warning, or Critical.");
        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("If-Match header must contain the current document version.");
    }
}
