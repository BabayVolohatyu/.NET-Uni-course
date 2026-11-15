using Catalog.Bll.Interfaces;
using Catalog.Bll.Queries;
using Catalog.Domain.Exceptions;
using Catalog.Grpc;
using Grpc.Core;

namespace Catalog.Api.Grpc;

/// <summary>
/// gRPC adapter for the Catalog (Contractor Registry) microservice.
/// Acts as a thin adapter over IContractorService — no business logic here.
/// Used by Platform.Aggregator for high-performance internal communication.
///
/// Error mapping:
///   NotFoundException   → StatusCode.NotFound
///   ConflictException   → StatusCode.AlreadyExists
///   ArgumentException   → StatusCode.InvalidArgument
///   Unexpected          → StatusCode.Internal
/// </summary>
public sealed class ContractorsGrpcService(
    IContractorService contractors,
    ILogger<ContractorsGrpcService> logger)
    : ContractorService.ContractorServiceBase
{
    public override async Task<ContractorReply> GetContractor(
        GetContractorRequest request,
        ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Contractor Id must be positive."));

        try
        {
            var dto = await contractors.GetByIdAsync(request.Id, context.CancellationToken);
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
            logger.LogError(ex, "Unexpected error in GetContractor for Id={Id}", request.Id);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task<ContractorsReply> GetContractors(
        GetContractorsRequest request,
        ServerCallContext context)
    {
        try
        {
            var query = new ContractorQuery
            {
                Page     = Math.Max(1, request.Page == 0 ? 1 : request.Page),
                PageSize = request.PageSize == 0 ? 20 : Math.Clamp(request.PageSize, 1, 100),
                IsActive = request.ActiveOnly ? true : (bool?)null
            };

            var paged = await contractors.GetListAsync(query, context.CancellationToken);

            var reply = new ContractorsReply();
            reply.Contractors.AddRange(paged.Items.Select(ToReply));
            return reply;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetContractors");
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task<ContractorsReply> GetContractorsByIds(
        GetContractorsByIdsRequest request,
        ServerCallContext context)
    {
        if (request.Ids.Count == 0)
            return new ContractorsReply();

        try
        {
            // Fetch each contractor; partial failures are skipped (NotFound → omit from result)
            var results = new List<ContractorReply>(request.Ids.Count);
            foreach (var id in request.Ids.Distinct())
            {
                try
                {
                    var dto = await contractors.GetByIdAsync(id, context.CancellationToken);
                    results.Add(ToReply(dto));
                }
                catch (NotFoundException)
                {
                    // Contractor not found — skip (batch operation, partial results acceptable)
                    logger.LogDebug("Contractor Id={Id} not found in batch fetch, skipping", id);
                }
            }

            var reply = new ContractorsReply();
            reply.Contractors.AddRange(results);
            return reply;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetContractorsByIds");
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static ContractorReply ToReply(Catalog.Bll.DTOs.ContractorWithDetailsDto dto) => new()
    {
        Id                = dto.Id,
        Name              = dto.Name,
        Edrpou            = dto.EDRPOU,
        AuthorizedCapital = (double)dto.AuthorizedCapital,
        RegistrationDate  = dto.RegistrationDate.ToString("O"),
        IsActive          = dto.IsActive,
        CreatedAt         = dto.CreatedAt.ToString("O")
    };

    private static ContractorReply ToReply(Catalog.Bll.DTOs.ContractorDto dto) => new()
    {
        Id                = dto.Id,
        Name              = dto.Name,
        Edrpou            = dto.EDRPOU,
        AuthorizedCapital = (double)dto.AuthorizedCapital,
        RegistrationDate  = dto.RegistrationDate.ToString("O"),
        IsActive          = dto.IsActive,
        CreatedAt         = dto.CreatedAt.ToString("O")
    };
}
