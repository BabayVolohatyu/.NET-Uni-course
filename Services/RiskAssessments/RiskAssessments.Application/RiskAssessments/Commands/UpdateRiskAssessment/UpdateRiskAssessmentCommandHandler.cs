using AutoMapper;
using MediatR;
using MongoDB.Bson;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.UpdateRiskAssessment;

public sealed class UpdateRiskAssessmentCommandHandler(
    IRiskAssessmentRepository assessments,
    IMapper mapper)
    : IRequestHandler<UpdateRiskAssessmentCommand, RiskAssessmentDetailsDto>
{
    public async Task<RiskAssessmentDetailsDto> Handle(UpdateRiskAssessmentCommand cmd, CancellationToken ct)
    {
        var id = ObjectId.Parse(cmd.Id);
        var assessment = await assessments.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Risk assessment {cmd.Id} was not found.");

        if (assessment.Version != cmd.ExpectedVersion)
        {
            throw new ConflictException(
                $"Risk assessment {cmd.Id} was modified by another auditor. Reload and send the current ETag.");
        }

        assessment.Revise(cmd.OverallRiskScore, Enum.Parse<RiskLevel>(cmd.RiskLevel, true));
        await assessments.UpdateAsync(assessment, ct);
        return mapper.Map<RiskAssessmentDetailsDto>(assessment);
    }
}
