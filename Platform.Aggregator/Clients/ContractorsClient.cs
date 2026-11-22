using Microsoft.Extensions.Logging;
using Platform.Aggregator.Dtos;
using System.Net.Http.Json;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HTTP client for Catalog.Api (catalog-api service).
/// Provides typed access to the contractor registry microservice.
/// BaseAddress "https+http://catalog-api" is resolved by Aspire service discovery.
/// </summary>
public sealed class ContractorsClient(HttpClient http, ILogger<ContractorsClient> logger)
{
    /// <summary>
    /// Get a page of contractors from the contractor registry.
    /// Used in the full tender aggregation to show participating contractors.
    /// </summary>
    public async Task<IReadOnlyList<ContractorSummaryDto>?> GetContractorsAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        logger.LogInformation("Calling catalog-api for contractors Page={Page} PageSize={PageSize}",
            page, pageSize);
        var result = await http.GetFromJsonAsync<ContractorsPagedResult>(
            $"/api/contractors?page={page}&pageSize={pageSize}", ct);
        return result?.Items;
    }

    /// <summary>Get a single contractor by ID.</summary>
    public async Task<ContractorSummaryDto?> GetContractorAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("Calling catalog-api for ContractorId={ContractorId}", id);
        return await http.GetFromJsonAsync<ContractorSummaryDto>($"/api/contractors/{id}", ct);
    }

    // Internal deserialization wrapper for the paged result envelope
    private record ContractorsPagedResult(
        IReadOnlyList<ContractorSummaryDto> Items,
        int Total,
        int Page,
        int PageSize);
}
