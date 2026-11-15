using Microsoft.Extensions.Logging;
using Platform.Aggregator.Dtos;
using System.Net.Http.Json;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HTTP client for Tenders.Api (tenders-api service).
/// Encapsulates all HTTP calls to the tender management microservice.
/// Service discovery resolves "https+http://tenders-api" to the real address via Aspire.
/// CorrelationIdHandler (registered in ServiceDefaults) automatically propagates X-Correlation-Id.
/// </summary>
public sealed class TendersClient(HttpClient http, ILogger<TendersClient> logger)
{
    /// <summary>Get a tender by ID. Returns null if not found.</summary>
    public async Task<TenderSummaryDto?> GetTenderAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Calling tenders-api for TenderId={TenderId}", id);
        return await http.GetFromJsonAsync<TenderSummaryDto>($"/api/tenders/{id}", ct);
    }

    /// <summary>
    /// Get all tenders for a specific procuring entity.
    /// Returns empty list on failure (handled by SafeAsync in the aggregation service).
    /// </summary>
    public async Task<IReadOnlyList<TenderSummaryDto>?> GetTendersByEntityAsync(
        long entityId, CancellationToken ct = default)
    {
        logger.LogInformation("Calling tenders-api for ProcuringEntityId={EntityId}", entityId);
        return await http.GetFromJsonAsync<IReadOnlyList<TenderSummaryDto>>(
            $"/api/tenders?procuringEntityId={entityId}", ct);
    }
}
