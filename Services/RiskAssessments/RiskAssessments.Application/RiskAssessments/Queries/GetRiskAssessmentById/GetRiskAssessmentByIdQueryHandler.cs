using MediatR;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Application.RiskAssessments.Dtos;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.Application.RiskAssessments.Queries.GetRiskAssessmentById;

public sealed class GetRiskAssessmentByIdQueryHandler(IRiskAssessmentQueries queries)
    : IRequestHandler<GetRiskAssessmentByIdQuery, RiskAssessmentDetailsDto>
{
    public async Task<RiskAssessmentDetailsDto> Handle(GetRiskAssessmentByIdQuery query, CancellationToken ct)
    {
        return await queries.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException($"Risk assessment {query.Id} was not found.");
    }
}
