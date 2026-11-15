using FluentValidation;

namespace RiskAssessments.Application.RiskAssessments.Queries.SearchRiskAssessments;

public sealed class SearchRiskAssessmentsQueryValidator : AbstractValidator<SearchRiskAssessmentsQuery>
{
    public SearchRiskAssessmentsQueryValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
