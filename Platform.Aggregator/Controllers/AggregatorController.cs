using Catalog.Grpc;
using Microsoft.AspNetCore.Authorization;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Dtos;
using Platform.Aggregator.Metrics;
using RiskAssessments.Grpc;
using System.Diagnostics;
using System.Text.Json;
using Tenders.Grpc;
using GrpcStatus = Grpc.Core.StatusCode;
using StackExchange.Redis;

namespace Platform.Aggregator.Controllers;

/// <summary>
/// Aggregator endpoints — combine data from multiple microservices in a single response.
///
/// Lab 06 upgrade:
///   • HTTP REST calls replaced by gRPC calls over HTTP/2 (lower latency, multiplexed).
///   • Parallel gRPC calls via Task.WhenAll — same pattern as before.
///   • RpcException (Unavailable / DeadlineExceeded) → Warnings, not 500.
///   • Aggregated responses cached in Redis with 30 s TTL.
///   • Cache checked BEFORE gRPC calls; partial responses are NOT cached.
/// </summary>
[ApiController]
[Authorize]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController(
    TendersService.TendersServiceClient tendersClient,
    ContractorService.ContractorServiceClient contractorClient,
    RiskAssessmentsService.RiskAssessmentsServiceClient riskClient,
    IDistributedCache cache,
    AggregatorCacheMetrics cacheMetrics,
    AggregationMetrics aggregationMetrics,
    ILogger<AggregatorController> logger) : ControllerBase
{
    // ActivitySource for custom business spans — visible in Aspire Dashboard "Traces"
    private static readonly ActivitySource Source = new("Platform.Aggregator");

    // Aggregated response TTL — shorter than individual service cache TTLs
    // to avoid serving stale aggregated data that looks "fresher" than its parts.
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Full tender view: tender data + participating contractors + risk assessments.
    ///
    /// Flow:
    ///   1. Check Redis cache → hit: return immediately (no gRPC calls needed)
    ///   2. gRPC GetTender → not found: return 404
    ///   3. Parallel gRPC calls for contractors and risk assessments (Task.WhenAll)
    ///   4. Cache complete result if no warnings; partial results are never cached.
    ///
    /// Example: GET /api/aggregator/tenders/42/full
    /// </summary>
    [HttpGet("tenders/{tenderId:long}/full")]
    [ProducesResponseType(typeof(FullTenderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullTenderDto>> GetFullTenderAsync(
        long tenderId,
        CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity("AggregateFullTender");
        activity?.SetTag("tender.id", tenderId);

        logger.LogInformation("Aggregating full tender view for TenderId={TenderId}", tenderId);
        var sw = Stopwatch.StartNew();

        var cacheKey = $"aggregator:tender:{tenderId}:full";

        // ── Step 0: Check Redis cache ──────────────────────────────────────────
        // Cache is optional. A connection or timeout failure falls through to the
        // services, which read SQL Server and MongoDB.
        try
        {
            var cached = await cache.GetAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                var cachedDto = JsonSerializer.Deserialize<FullTenderDto>(cached);
                if (cachedDto is not null)
                {
                    cacheMetrics.RecordHit(cacheKey);
                    logger.LogDebug("Aggregator cache hit for key {CacheKey}", cacheKey);
                    activity?.SetTag("cache.hit", true);
                    aggregationMetrics.RecordDuration("assemble", success: true, sw.Elapsed.TotalMilliseconds);
                    return Ok(cachedDto);
                }
            }
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Redis unavailable, falling back to database for {Key}", cacheKey);
        }

        cacheMetrics.RecordMiss(cacheKey);

        var warnings = new List<string>();

        // ── Step 1: Fetch the tender via gRPC ─────────────────────────────────
        TenderReply? tender;
        try
        {
            tender = await tendersClient.GetTenderAsync(
                new GetTenderRequest { Id = tenderId },
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == GrpcStatus.NotFound)
        {
            logger.LogWarning("Tender {TenderId} not found in tenders-api (gRPC NotFound)", tenderId);
            aggregationMetrics.RecordDuration("assemble", success: false, sw.Elapsed.TotalMilliseconds);
            return NotFound(new ProblemDetails
            {
                Title  = "Tender not found",
                Detail = $"Tender with Id={tenderId} does not exist.",
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "tenders-api gRPC call failed for TenderId={TenderId}", tenderId);
            aggregationMetrics.RecordDuration("assemble", success: false, sw.Elapsed.TotalMilliseconds);
            return NotFound(new ProblemDetails
            {
                Title  = "Tender not found",
                Detail = $"Could not retrieve tender Id={tenderId}: {ex.Status.Detail}",
                Status = StatusCodes.Status404NotFound
            });
        }

        activity?.SetTag("tender.title", tender.Title);
        activity?.SetTag("tender.status", tender.Status);

        // ── Step 2: Parallel gRPC calls for contractors and risk assessments ──
        var contractorsTask = SafeGrpcAsync(
            () => contractorClient.GetContractorsAsync(
                new GetContractorsRequest { Page = 1, PageSize = 100 },
                cancellationToken: cancellationToken).ResponseAsync,
            "catalog-api", warnings);

        var riskTask = SafeGrpcAsync(
            () => riskClient.GetRiskAssessmentsByTenderAsync(
                new GetByTenderRequest { TenderId = tenderId.ToString() },
                cancellationToken: cancellationToken).ResponseAsync,
            "risk-assessments-api", warnings);

        // ⚠️ Parallel execution over HTTP/2 multiplexed channel — both calls use the same connection.
        await Task.WhenAll(contractorsTask, riskTask);

        var contractorsReply     = await contractorsTask;
        var riskAssessmentsReply = await riskTask;

        // ── Step 3: Map to DTOs ────────────────────────────────────────────────
        var contractorDtos = (contractorsReply?.Contractors ?? [])
            .Select(MapContractor).ToList();

        var riskDtos = (riskAssessmentsReply?.Assessments ?? [])
            .Select(MapRiskAssessment).ToList();

        activity?.SetTag("contractors.count", contractorDtos.Count);
        activity?.SetTag("risk-assessments.count", riskDtos.Count);
        activity?.SetTag("warnings.count", warnings.Count);

        logger.LogInformation(
            "Aggregation complete for TenderId={TenderId}: " +
            "Contractors={ContractorsCount} RiskAssessments={RiskCount} Warnings={WarningsCount}",
            tenderId, contractorDtos.Count, riskDtos.Count, warnings.Count);

        var tenderSummary = MapTender(tender);
        var result = new FullTenderDto(tenderSummary, contractorDtos, riskDtos, warnings);
        aggregationMetrics.DossierAssembled(partial: warnings.Count > 0);

        // ── Step 4: Cache complete result (not partial) ───────────────────────
        if (warnings.Count == 0)
        {
            try
            {
                await cache.SetAsync(
                    cacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(result),
                    CacheOptions,
                    cancellationToken);
                logger.LogDebug("Aggregated result cached with key {CacheKey}", cacheKey);
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
            {
                logger.LogWarning(ex, "Redis unavailable, value for {Key} not cached", cacheKey);
            }
        }
        else
        {
            logger.LogDebug("Partial result for TenderId={TenderId} not cached (has warnings)", tenderId);
        }

        aggregationMetrics.RecordDuration("assemble", success: true, sw.Elapsed.TotalMilliseconds);
        return Ok(result);
    }

    /// <summary>
    /// Platform health aggregation: checks all three microservices in parallel via gRPC.
    /// Returns overall status and per-service status with warnings for unavailable services.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(PlatformHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformHealthDto>> GetPlatformHealthAsync(
        CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity("AggregatePlatformHealth");

        var warnings = new List<string>();
        var services = new List<ServiceHealthDto>();

        // Check all services in parallel via gRPC
        var tendersHealthTask = SafeGrpcAsync(
            () => tendersClient.GetTenderAsync(
                new GetTenderRequest { Id = 1 },
                cancellationToken: cancellationToken).ResponseAsync,
            "tenders-api", warnings);

        var catalogHealthTask = SafeGrpcAsync(
            () => contractorClient.GetContractorsAsync(
                new GetContractorsRequest { Page = 1, PageSize = 1 },
                cancellationToken: cancellationToken).ResponseAsync,
            "catalog-api", warnings);

        var riskHealthTask = SafeGrpcAsync(
            () => riskClient.GetRiskAssessmentsAsync(
                new GetRiskAssessmentsRequest { Page = 1, PageSize = 1 },
                cancellationToken: cancellationToken).ResponseAsync,
            "risk-assessments-api", warnings);

        await Task.WhenAll(tendersHealthTask, catalogHealthTask, riskHealthTask);

        services.Add(new ServiceHealthDto("tenders-api",
            (await tendersHealthTask) is not null ? "Healthy" : "Degraded", null));
        services.Add(new ServiceHealthDto("catalog-api",
            (await catalogHealthTask) is not null ? "Healthy" : "Degraded", null));
        services.Add(new ServiceHealthDto("risk-assessments-api",
            (await riskHealthTask) is not null ? "Healthy" : "Degraded", null));

        var overallStatus = warnings.Count == 0 ? "Healthy"
            : warnings.Count < services.Count ? "Degraded"
            : "Unhealthy";

        return Ok(new PlatformHealthDto(overallStatus, services, warnings));
    }

    // ── SafeGrpcAsync helper ──────────────────────────────────────────────────────
    /// <summary>
    /// Wraps a gRPC async call to handle partial failures gracefully.
    /// On RpcException (Unavailable / DeadlineExceeded) or TaskCanceledException:
    ///   • logs a warning with the service name and status code
    ///   • adds a warning to the shared list
    ///   • returns default (null) instead of re-throwing
    /// This ensures one unavailable microservice does not break the entire aggregation.
    /// </summary>
    private async Task<T?> SafeGrpcAsync<T>(
        Func<Task<T>> call,
        string serviceName,
        List<string> warnings)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{ServiceName} gRPC responded in {ElapsedMs} ms",
                serviceName, sw.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex) when (
            ex.StatusCode is GrpcStatus.Unavailable or GrpcStatus.DeadlineExceeded
                          or GrpcStatus.Cancelled or GrpcStatus.NotFound)
        {
            logger.LogWarning(ex,
                "{ServiceName} gRPC call failed: {Status} after {ElapsedMs} ms",
                serviceName, ex.StatusCode, sw.ElapsedMilliseconds);
            lock (warnings)
                warnings.Add($"{serviceName} is unavailable: {ex.StatusCode} — {ex.Status.Detail}");
            return default;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "{ServiceName} gRPC call timed out after {ElapsedMs} ms",
                serviceName, sw.ElapsedMilliseconds);
            lock (warnings)
                warnings.Add($"{serviceName} timed out after {sw.ElapsedMilliseconds} ms");
            return default;
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static TenderSummaryDto MapTender(TenderReply t) => new(
        t.Id,
        t.Title,
        t.Status,
        (decimal)t.ExpectedBudget,
        ProcuringEntityName: string.Empty,  // not carried in gRPC response (denormalized at source)
        CategoryCode: null,
        string.IsNullOrEmpty(t.PublishedAt) ? null : DateTime.Parse(t.PublishedAt));

    private static ContractorSummaryDto MapContractor(ContractorReply c) => new(
        c.Id,
        c.Name,
        c.Edrpou,
        c.IsActive,
        (decimal)c.AuthorizedCapital,
        Region: null);

    private static RiskAssessmentSummaryDto MapRiskAssessment(RiskAssessmentReply r) => new(
        r.Id,
        r.TenderId.ToString(),
        r.TenderTitle,
        r.RiskLevel,
        r.FlagsCount,
        TriggeredFlags: null);
}
