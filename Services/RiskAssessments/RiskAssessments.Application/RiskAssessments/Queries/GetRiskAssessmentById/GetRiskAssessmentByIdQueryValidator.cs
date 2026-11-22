using FluentValidation;
using MongoDB.Bson;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentById;

public sealed class GetRiskAssessmentByIdQueryValidator : AbstractValidator<GetRiskAssessmentByIdQuery>
{
    public GetRiskAssessmentByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId.");
    }
}
