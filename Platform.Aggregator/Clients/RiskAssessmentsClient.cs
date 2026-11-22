using Microsoft.Extensions.Logging;
using Platform.Aggregator.Dtos;
using System.Net.Http.Json;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HTTP client for RiskAssessments.WebApi (risk-assessments-api service).
/// Provides typed access to the risk assessment microservice backed by MongoDB.
/// BaseAddress "https+http://risk-assessments-api" is resolved by Aspire service discovery.
/// </summary>
public sealed class RiskAssessmentsClient(HttpClient http, ILogger<RiskAssessmentsClient> logger)
{
    /// <summary>Get all risk assessments for a specific tender ID.</summary>
    public async Task<IReadOnlyList<RiskAssessmentSummaryDto>?> GetByTenderAsync(
        string tenderId,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Calling risk-assessments-api for TenderId={TenderId}", tenderId);
        return await http.GetFromJsonAsync<IReadOnlyList<RiskAssessmentSummaryDto>>(
            $"/api/risk-assessments?search={tenderId}", ct);
    }

    /// <summary>Get a risk assessment by its MongoDB ObjectId.</summary>
    public async Task<RiskAssessmentSummaryDto?> GetByIdAsync(
        string id,
        CancellationToken ct = default)
    {
        logger.LogInformation("Calling risk-assessments-api for AssessmentId={AssessmentId}", id);
        return await http.GetFromJsonAsync<RiskAssessmentSummaryDto>(
            $"/api/risk-assessments/{id}", ct);
    }

    /// <summary>Get a list of risk assessments (paged).</summary>
    public async Task<IReadOnlyList<RiskAssessmentSummaryDto>?> GetListAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Calling risk-assessments-api list Page={Page} PageSize={PageSize}", page, pageSize);
        var result = await http.GetFromJsonAsync<RiskAssessmentsPagedResult>(
            $"/api/risk-assessments?page={page}&pageSize={pageSize}", ct);
        return result?.Items;
    }

    private record RiskAssessmentsPagedResult(
        IReadOnlyList<RiskAssessmentSummaryDto> Items,
        int Total,
        int Page,
        int PageSize);
}
