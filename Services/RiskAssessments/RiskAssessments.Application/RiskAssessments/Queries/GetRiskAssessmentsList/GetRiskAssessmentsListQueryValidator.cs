using FluentValidation;
using RiskAssessments.Domain.Enums;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsList;

public sealed class GetRiskAssessmentsListQueryValidator : AbstractValidator<GetRiskAssessmentsListQuery>
{
    public GetRiskAssessmentsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ProcuringEntityId).GreaterThan(0).When(x => x.ProcuringEntityId is not null);
        RuleFor(x => x.RiskLevel)
            .Must(level => string.IsNullOrWhiteSpace(level)
                           || (Enum.TryParse<RiskLevel>(level, true, out var parsed) && Enum.IsDefined(parsed)))
            .WithMessage("RiskLevel must be Low, Medium, High, or Critical.");
    }
}
