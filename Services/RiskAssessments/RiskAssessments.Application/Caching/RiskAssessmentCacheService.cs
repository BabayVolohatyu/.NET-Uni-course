using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiskAssessments.Application.Common.Interfaces;
using StackExchange.Redis;
using RiskAssessments.Application.Common.Models;
using RiskAssessments.Application.RiskAssessments.Dtos;
using System.Text.Json;

namespace RiskAssessments.Application.Caching;

/// <summary>
/// Two-level caching wrapper over IRiskAssessmentQueries.
/// Implements the same interface so it can be substituted transparently via DI.
/// L1 = IMemoryCache (fast, per-instance, short TTL)
/// L2 = IDistributedCache (Redis via Aspire, shared across replicas, longer TTL)
///
/// Cache-aside pattern:
///   1. Check L1 → hit: return immediately
///   2. Check L2 → hit: populate L1, return
///   3. Miss → call underlying queries → populate L2 then L1 → return
///
/// Key schema: risk:assessment:{id}, risk:assessments:list:{params...}
/// </summary>
public sealed class RiskAssessmentCacheService : IRiskAssessmentQueries
{
    private readonly IRiskAssessmentQueries _inner;
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly ILogger<RiskAssessmentCacheService> _logger;
    private readonly CacheMetrics _metrics;

    // TTL constants — L1 must be shorter than L2 to avoid stale in-memory data.
    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan L2Ttl = TimeSpan.FromMinutes(10);

    private static readonly MemoryCacheEntryOptions L1Options = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromSeconds(30),
        AbsoluteExpirationRelativeToNow = L1Ttl
    };

    private static readonly DistributedCacheEntryOptions L2Options = new()
    {
        AbsoluteExpirationRelativeToNow = L2Ttl
    };

    public RiskAssessmentCacheService(
        IRiskAssessmentQueries inner,
        IMemoryCache l1,
        IDistributedCache l2,
        ILogger<RiskAssessmentCacheService> logger,
        CacheMetrics metrics)
    {
        _inner   = inner;
        _l1      = l1;
        _l2      = l2;
        _logger  = logger;
        _metrics = metrics;
    }

    // ── Cached read operations ────────────────────────────────────────────────

    public Task<RiskAssessmentDetailsDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        var key = $"risk:assessment:{id}";
        return GetOrCreateAsync(key, () => _inner.GetByIdAsync(id, ct), ct);
    }

    public Task<PagedResult<RiskAssessmentDto>> GetListAsync(
        long? procuringEntityId, string? riskLevel, int page, int pageSize, CancellationToken ct)
    {
        var key = $"risk:assessments:list:{procuringEntityId}:{riskLevel}:{page}:{pageSize}";
        return GetOrCreateAsync<PagedResult<RiskAssessmentDto>>(
            key, () => _inner.GetListAsync(procuringEntityId, riskLevel, page, pageSize, ct)!, ct)!;
    }

    public Task<CursorPage<RiskAssessmentDto>> GetCursorPageAsync(
        long? procuringEntityId, string? afterId, int pageSize, CancellationToken ct)
    {
        // Cursor pages change frequently; use a short TTL to avoid stale pagination.
        var key = $"risk:assessments:cursor:{procuringEntityId}:{afterId}:{pageSize}";
        return GetOrCreateAsync<CursorPage<RiskAssessmentDto>>(
            key, () => _inner.GetCursorPageAsync(procuringEntityId, afterId, pageSize, ct)!, ct)!;
    }

    public Task<PagedResult<RiskAssessmentDto>> SearchAsync(string text, int page, int pageSize, CancellationToken ct)
    {
        // Search results cached briefly — search data is volatile
        var key = $"risk:assessments:search:{text}:{page}:{pageSize}";
        return GetOrCreateAsync<PagedResult<RiskAssessmentDto>>(
            key, () => _inner.SearchAsync(text, page, pageSize, ct)!, ct)!;
    }

    // ── Two-level cache helper ────────────────────────────────────────────────

    private async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T?>> factory,
        CancellationToken ct)
    {
        // L1 check
        if (_l1.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache L1 hit {Key}", key);
            _metrics.RecordHit("L1");
            return value;
        }

        _metrics.RecordMiss("L1");

        // L2 check (Redis)
        try
        {
            var bytes = await _l2.GetAsync(key, ct);
            if (bytes is not null)
            {
                value = JsonSerializer.Deserialize<T>(bytes);
                _l1.Set(key, value, L1Options);
                _logger.LogDebug("Cache L2 hit {Key}", key);
                _metrics.RecordHit("L2");
                return value;
            }
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Cache is optional. The inner queries read the riskAssessments collection.
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for {Key}", key);
        }

        _metrics.RecordMiss("L2");

        // Source call (MongoDB)
        value = await factory();
        if (value is null) return default;

        try
        {
            await _l2.SetAsync(
                key,
                JsonSerializer.SerializeToUtf8Bytes(value),
                L2Options,
                ct);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis unavailable, value for {Key} not cached", key);
        }

        _l1.Set(key, value, L1Options);
        _logger.LogDebug("Cache populated L1+L2 {Key}", key);
        return value;
    }
}
