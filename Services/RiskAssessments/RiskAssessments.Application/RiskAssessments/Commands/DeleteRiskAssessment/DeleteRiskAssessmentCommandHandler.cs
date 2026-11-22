using MediatR;
using MongoDB.Bson;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.DeleteRiskAssessment;

public sealed class DeleteRiskAssessmentCommandHandler(IRiskAssessmentRepository assessments)
    : IRequestHandler<DeleteRiskAssessmentCommand>
{
    public async Task Handle(DeleteRiskAssessmentCommand cmd, CancellationToken ct)
    {
        await assessments.DeleteAsync(ObjectId.Parse(cmd.Id), cmd.ExpectedVersion, ct);
    }
}
