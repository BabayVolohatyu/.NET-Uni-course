using Grpc.Core;
using MediatR;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Grpc;

namespace RiskAssessments.WebApi.Grpc;

/// <summary>
/// gRPC adapter for the RiskAssessments microservice.
/// Acts as a thin adapter over IRiskAssessmentQueries — no business logic here.
/// Used by Platform.Aggregator for high-performance internal communication.
///
/// Error mapping:
///   NotFoundException   → StatusCode.NotFound
///   ArgumentException   → StatusCode.InvalidArgument
///   Unexpected          → StatusCode.Internal
/// </summary>
public sealed class RiskAssessmentsGrpcService(
    IRiskAssessmentQueries queries,
    ILogger<RiskAssessmentsGrpcService> logger)
    : RiskAssessmentsService.RiskAssessmentsServiceBase
{
    public override async Task<RiskAssessmentReply> GetRiskAssessment(
        GetRiskAssessmentRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "RiskAssessment Id must not be empty."));

        try
        {
            var dto = await queries.GetByIdAsync(request.Id, context.CancellationToken);
            if (dto is null)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"RiskAssessment with Id={request.Id} was not found."));

            return ToReply(dto);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetRiskAssessment for Id={Id}", request.Id);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task<RiskAssessmentsReply> GetRiskAssessmentsByTender(
        GetByTenderRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TenderId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "TenderId must not be empty."));

        try
        {
            // Parse tender ID — stored as string in gRPC contract (compatible with long IDs)
            if (!long.TryParse(request.TenderId, out var tenderId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "TenderId must be a numeric string."));

            var paged = await queries.GetListAsync(null, null, 1, 100, context.CancellationToken);
            var byTender = paged.Items
                .Where(a => a.TenderId == tenderId)
                .ToList();

            var reply = new RiskAssessmentsReply();
            reply.Assessments.AddRange(byTender.Select(a => new RiskAssessmentReply
            {
                Id                  = a.Id,
                TenderId            = a.TenderId,
                TenderTitle         = a.TenderTitle,
                ProcuringEntityId   = a.ProcuringEntityId,
                ProcuringEntityName = a.ProcuringEntityName,
                AuditorName         = a.AuditorName,
                OverallRiskScore    = a.OverallRiskScore,
                RiskLevel           = a.RiskLevel,
                FlagsCount          = a.FlagsCount,
                Version             = a.Version,
                CreatedAt           = a.CreatedAt.ToString("O")
            }));
            return reply;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetRiskAssessmentsByTender for TenderId={TenderId}",
                request.TenderId);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task<RiskAssessmentsReply> GetRiskAssessments(
        GetRiskAssessmentsRequest request,
        ServerCallContext context)
    {
        try
        {
            var page     = Math.Max(1, request.Page == 0 ? 1 : request.Page);
            var pageSize = request.PageSize == 0 ? 20 : Math.Clamp(request.PageSize, 1, 100);

            var paged = await queries.GetListAsync(null, null, page, pageSize, context.CancellationToken);

            var reply = new RiskAssessmentsReply();
            reply.Assessments.AddRange(paged.Items.Select(a => new RiskAssessmentReply
            {
                Id                  = a.Id,
                TenderId            = a.TenderId,
                TenderTitle         = a.TenderTitle,
                ProcuringEntityId   = a.ProcuringEntityId,
                ProcuringEntityName = a.ProcuringEntityName,
                AuditorName         = a.AuditorName,
                OverallRiskScore    = a.OverallRiskScore,
                RiskLevel           = a.RiskLevel,
                FlagsCount          = a.FlagsCount,
                Version             = a.Version,
                CreatedAt           = a.CreatedAt.ToString("O")
            }));
            return reply;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetRiskAssessments");
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static RiskAssessmentReply ToReply(RiskAssessments.Application.RiskAssessments.Dtos.RiskAssessmentDetailsDto dto) =>
        new()
        {
            Id                  = dto.Id,
            TenderId            = dto.TenderId,
            TenderTitle         = dto.TenderTitle,
            ProcuringEntityId   = dto.ProcuringEntityId,
            ProcuringEntityName = dto.ProcuringEntityName,
            AuditorName         = dto.AuditorName,
            OverallRiskScore    = dto.OverallRiskScore,
            RiskLevel           = dto.RiskLevel,
            FlagsCount          = dto.TriggeredFlags?.Count ?? 0,
            Version             = dto.Version,
            CreatedAt           = dto.CreatedAt.ToString("O")
        };
}
