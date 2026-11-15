using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Dtos;
using System.Diagnostics;

namespace Platform.Aggregator.Controllers;

/// <summary>
/// Aggregator endpoints — combine data from multiple microservices in a single response.
///
/// Pattern: Aggregator receives a request → fires parallel calls to Tenders.Api,
/// Catalog.Api, and RiskAssessments.WebApi via Task.WhenAll → assembles FullTenderDto.
///
/// Partial failures: if one downstream service is unavailable, the response still returns
/// data from the available services plus a Warnings list — HTTP 200, not 500.
/// </summary>
[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController(
    TendersClient tendersClient,
    ContractorsClient contractorsClient,
    RiskAssessmentsClient riskAssessmentsClient,
    ILogger<AggregatorController> logger) : ControllerBase
{
    // ActivitySource for custom business spans — visible in Aspire Dashboard "Traces"
    private static readonly ActivitySource Source = new("Platform.Aggregator");

    /// <summary>
    /// Full tender view: tender data + participating contractors + risk assessments.
    ///
    /// Calls Tenders.Api, Catalog.Api, and RiskAssessments.WebApi in parallel (Task.WhenAll).
    /// If Catalog.Api or RiskAssessments.WebApi is unavailable, returns partial data with Warnings.
    /// If Tenders.Api is unavailable, returns 404 (no tender = nothing to aggregate).
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

        logger.LogInformation(
            "Aggregating full tender view for TenderId={TenderId}", tenderId);

        var warnings = new List<string>();

        // Step 1: Fetch the tender — without it there's nothing to aggregate
        var tender = await tendersClient.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null)
        {
            logger.LogWarning("Tender {TenderId} not found in tenders-api", tenderId);
            return NotFound(new ProblemDetails
            {
                Title = "Tender not found",
                Detail = $"Tender with Id={tenderId} does not exist.",
                Status = StatusCodes.Status404NotFound
            });
        }

        activity?.SetTag("tender.title", tender.Title);
        activity?.SetTag("tender.status", tender.Status);

        // Step 2: Fetch contractors and risk assessments in PARALLEL (Task.WhenAll)
        // CancellationToken is passed to all calls — if the client disconnects, all calls stop.
        // Partial failures are caught by SafeAsync — unavailable service → warning, not 500.
        var contractorsTask    = SafeAsync(
            () => contractorsClient.GetContractorsAsync(cancellationToken: cancellationToken),
            "catalog-api", warnings, cancellationToken);

        var riskAssessmentsTask = SafeAsync(
            () => riskAssessmentsClient.GetByTenderAsync(tenderId.ToString(), cancellationToken),
            "risk-assessments-api", warnings, cancellationToken);

        // ⚠️ Task.WhenAll — parallel execution (~max(t1,t2) instead of t1+t2)
        // ⚠️ NOT Task.WaitAll or .Result — those block threads and reduce scalability
        await Task.WhenAll(contractorsTask, riskAssessmentsTask);

        var contractors    = await contractorsTask    ?? [];
        var riskAssessments = await riskAssessmentsTask ?? [];

        activity?.SetTag("contractors.count", contractors.Count);
        activity?.SetTag("risk-assessments.count", riskAssessments.Count);
        activity?.SetTag("warnings.count", warnings.Count);

        logger.LogInformation(
            "Aggregation complete for TenderId={TenderId}: " +
            "Contractors={ContractorsCount} RiskAssessments={RiskAssessmentsCount} Warnings={WarningsCount}",
            tenderId, contractors.Count, riskAssessments.Count, warnings.Count);

        return Ok(new FullTenderDto(tender, contractors, riskAssessments, warnings));
    }

    /// <summary>
    /// Platform health aggregation: checks all three microservices in parallel.
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

        // Check all services in parallel
        var tendersHealthTask        = SafeAsync(
            () => tendersClient.GetTenderAsync(1, cancellationToken),
            "tenders-api", warnings, cancellationToken);
        var contractorsHealthTask    = SafeAsync(
            () => contractorsClient.GetContractorsAsync(cancellationToken: cancellationToken),
            "catalog-api", warnings, cancellationToken);
        var riskHealthTask           = SafeAsync(
            () => riskAssessmentsClient.GetListAsync(cancellationToken: cancellationToken),
            "risk-assessments-api", warnings, cancellationToken);

        await Task.WhenAll(tendersHealthTask, contractorsHealthTask, riskHealthTask);

        services.Add(new ServiceHealthDto("tenders-api",
            (await tendersHealthTask) is not null ? "Healthy" : "Degraded", null));
        services.Add(new ServiceHealthDto("catalog-api",
            (await contractorsHealthTask) is not null ? "Healthy" : "Degraded", null));
        services.Add(new ServiceHealthDto("risk-assessments-api",
            (await riskHealthTask) is not null ? "Healthy" : "Degraded", null));

        var overallStatus = warnings.Count == 0 ? "Healthy"
            : warnings.Count < services.Count ? "Degraded"
            : "Unhealthy";

        return Ok(new PlatformHealthDto(overallStatus, services, warnings));
    }

    // ── SafeAsync helper ──────────────────────────────────────────────────────────
    /// <summary>
    /// Wraps an async service call to handle partial failures gracefully.
    /// On HttpRequestException or TaskCanceledException (service unavailable / timeout):
    ///   • logs a warning with the service name and elapsed time
    ///   • adds a warning to the shared list
    ///   • returns default (null) instead of re-throwing
    /// This ensures one unavailable service does not break the entire aggregation.
    /// </summary>
    private async Task<T?> SafeAsync<T>(
        Func<Task<T?>> call,
        string serviceName,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{ServiceName} responded in {ElapsedMs} ms",
                serviceName, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                           or OperationCanceledException)
        {
            logger.LogWarning(ex,
                "{ServiceName} is unavailable after {ElapsedMs} ms",
                serviceName, sw.ElapsedMilliseconds);

            // Thread-safe addition (multiple tasks may finish around the same time)
            lock (warnings)
                warnings.Add($"{serviceName} is unavailable: {ex.Message}");

            return default;
        }
    }
}
