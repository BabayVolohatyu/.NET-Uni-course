using FluentValidation;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.CreateRiskAssessment;

public sealed class CreateRiskAssessmentCommandValidator : AbstractValidator<CreateRiskAssessmentCommand>
{
    public CreateRiskAssessmentCommandValidator(IRiskAssessmentRepository assessments)
    {
        RuleFor(x => x.TenderId).GreaterThan(0);
        RuleFor(x => x.TenderTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ProcuringEntityId).GreaterThan(0);
        RuleFor(x => x.ProcuringEntityName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuditorId).NotEmpty().MaximumLength(80);
        RuleFor(x => x.AuditorName).NotEmpty().MaximumLength(100);
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
        RuleFor(x => x.ExpectedBudgetCurrency)
            .Must(currency => string.IsNullOrWhiteSpace(currency) || currency.Trim().Length == 3)
            .WithMessage("ExpectedBudgetCurrency must be a 3-letter ISO code.");
        RuleFor(x => x.ExpectedBudgetAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedBudgetAmount is not null);

        RuleFor(x => x.TenderId)
            .MustAsync(async (tenderId, ct) => !await assessments.ExistsAsync(tenderId, ct))
            .WithMessage("An assessment for this tender already exists.");
    }
}
