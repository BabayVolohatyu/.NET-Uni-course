using FluentValidation;
using MongoDB.Bson;
using RiskAssessments.Domain.Enums;

namespace RiskAssessments.Application.RiskAssessments.Commands.UpdateRiskAssessment;

public sealed class UpdateRiskAssessmentCommandValidator : AbstractValidator<UpdateRiskAssessmentCommand>
{
    public UpdateRiskAssessmentCommandValidator()
    {
        RuleFor(x => x.Id)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId.");
        RuleFor(x => x.OverallRiskScore).InclusiveBetween(0, 100);
        RuleFor(x => x.RiskLevel)
            .Must(level => Enum.TryParse<RiskLevel>(level, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("RiskLevel must be Low, Medium, High, or Critical.");
        RuleFor(x => x)
            .Must(cmd =>
            {
                if (!Enum.TryParse<RiskLevel>(cmd.RiskLevel, true, out var level))
                    return true;
                return Domain.Entities.RiskAssessment.LevelMatchesScore(cmd.OverallRiskScore, level);
            })
            .WithMessage("Risk level does not match the score band (Low 0–29, Medium 30–54, High 55–79, Critical 80–100).");
        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("If-Match header must contain the current document version.");
    }
}
