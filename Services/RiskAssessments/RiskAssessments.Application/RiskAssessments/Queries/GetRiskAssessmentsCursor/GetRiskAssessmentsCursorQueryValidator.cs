using FluentValidation;
using MongoDB.Bson;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentsCursor;

public sealed class GetRiskAssessmentsCursorQueryValidator : AbstractValidator<GetRiskAssessmentsCursorQuery>
{
    public GetRiskAssessmentsCursorQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ProcuringEntityId).GreaterThan(0).When(x => x.ProcuringEntityId is not null);
        RuleFor(x => x.AfterId)
            .Must(id => string.IsNullOrWhiteSpace(id) || ObjectId.TryParse(id, out _))
            .WithMessage("afterId must be a valid ObjectId.");
    }
}
