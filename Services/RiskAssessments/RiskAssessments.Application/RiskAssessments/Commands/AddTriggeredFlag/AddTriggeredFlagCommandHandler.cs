using AutoMapper;
using MediatR;
using MongoDB.Bson;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.Interfaces;

namespace RiskAssessments.Application.RiskAssessments.Commands.AddTriggeredFlag;

public sealed class AddTriggeredFlagCommandHandler(
    IRiskAssessmentRepository assessments,
    IMapper mapper)
    : IRequestHandler<AddTriggeredFlagCommand, RiskAssessmentDetailsDto>
{
    public async Task<RiskAssessmentDetailsDto> Handle(AddTriggeredFlagCommand cmd, CancellationToken ct)
    {
        var id = ObjectId.Parse(cmd.AssessmentId);
        var assessment = await assessments.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Risk assessment {cmd.AssessmentId} was not found.");

        if (assessment.Version != cmd.ExpectedVersion)
        {
            throw new ConflictException(
                $"Risk assessment {cmd.AssessmentId} was modified by another auditor. Reload and send the current ETag.");
        }

        assessment.AddFlag(new TriggeredFlag(
            cmd.RuleCode,
            Enum.Parse<FlagSeverity>(cmd.Severity, true),
            cmd.Description,
            cmd.AffectedField,
            cmd.ContractorId,
            cmd.CompanyAgeDays,
            cmd.AuthorizedCapital));

        await assessments.UpdateAsync(assessment, ct);
        return mapper.Map<RiskAssessmentDetailsDto>(assessment);
    }
}
