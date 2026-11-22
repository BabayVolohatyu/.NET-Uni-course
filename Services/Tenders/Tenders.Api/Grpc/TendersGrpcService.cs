using Grpc.Core;
using Tenders.Bll.Interfaces;
using Tenders.Domain.Exceptions;
using Tenders.Grpc;

namespace Tenders.Api.Grpc;

/// <summary>
/// gRPC adapter for the Tenders microservice.
/// Acts as a thin adapter over ITenderService — no business logic here.
/// Used by Platform.Aggregator for high-performance internal communication.
///
/// Error mapping:
///   NotFoundException       → StatusCode.NotFound
///   ArgumentException       → StatusCode.InvalidArgument
///   ValidationException     → StatusCode.InvalidArgument
///   Unexpected exceptions   → StatusCode.Internal (re-throw)
/// </summary>
public sealed class TendersGrpcService(
    ITenderService tenders,
    ILogger<TendersGrpcService> logger)
    : TendersService.TendersServiceBase
{
    public override async Task<TenderReply> GetTender(
        GetTenderRequest request,
        ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tender Id must be positive."));

        try
        {
            var dto = await tenders.GetTenderByIdAsync(request.Id, context.CancellationToken);
            if (dto is null)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Tender with Id={request.Id} was not found."));

            return ToReply(dto);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (RpcException)
        {
            throw; // already wrapped
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetTender for Id={Id}", request.Id);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task<TendersReply> GetTendersByEntity(
        GetTendersByEntityRequest request,
        ServerCallContext context)
    {
        if (request.ProcuringEntityId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "ProcuringEntityId must be positive."));

        try
        {
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status;
            var dtos = await tenders.GetTendersByEntityAsync(
                request.ProcuringEntityId, status, context.CancellationToken);

            var reply = new TendersReply();
            reply.Tenders.AddRange(dtos.Select(ToReply));
            return reply;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetTendersByEntity for EntityId={Id}",
                request.ProcuringEntityId);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static TenderReply ToReply(Tenders.Bll.Dtos.TenderWithDetailsDto dto) => new()
    {
        Id                 = dto.Id,
        ProcuringEntityId  = dto.ProcuringEntityId,
        Title              = dto.Title,
        ExpectedBudget     = (double)dto.ExpectedBudget,
        Status             = dto.Status,
        PublishedAt        = dto.PublishedAt?.ToString("O") ?? string.Empty,
        BiddingDeadline    = dto.BiddingDeadline?.ToString("O") ?? string.Empty,
        CreatedAt          = dto.CreatedAt.ToString("O")
    };

    private static TenderReply ToReply(Tenders.Bll.Dtos.TenderDto dto) => new()
    {
        Id                 = dto.Id,
        ProcuringEntityId  = dto.ProcuringEntityId,
        Title              = dto.Title,
        ExpectedBudget     = (double)dto.ExpectedBudget,
        Status             = dto.Status,
        PublishedAt        = dto.PublishedAt?.ToString("O") ?? string.Empty,
        BiddingDeadline    = dto.BiddingDeadline?.ToString("O") ?? string.Empty,
        CreatedAt          = dto.CreatedAt.ToString("O")
    };
}
