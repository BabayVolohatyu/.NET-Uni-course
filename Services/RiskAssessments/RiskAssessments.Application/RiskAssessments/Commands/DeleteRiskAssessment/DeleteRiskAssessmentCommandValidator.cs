using FluentValidation;
using MongoDB.Bson;

namespace RiskAssessments.Application.RiskAssessments.Commands.DeleteRiskAssessment;

public sealed class DeleteRiskAssessmentCommandValidator : AbstractValidator<DeleteRiskAssessmentCommand>
{
    public DeleteRiskAssessmentCommandValidator()
    {
        RuleFor(x => x.Id)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId.");
        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("If-Match header must contain the current document version.");
    }
}
