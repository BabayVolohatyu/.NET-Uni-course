using FluentValidation;
using MongoDB.Bson;

namespace RiskAssessments.Application.AuditNotes.Queries.GetAuditNotesByAssessment;

public sealed class GetAuditNotesByAssessmentQueryValidator : AbstractValidator<GetAuditNotesByAssessmentQuery>
{
    public GetAuditNotesByAssessmentQueryValidator()
    {
        RuleFor(x => x.RiskAssessmentId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("RiskAssessmentId must be a valid ObjectId.");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
