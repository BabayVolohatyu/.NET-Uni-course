using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;

namespace Tenders.Bll.Caching;

/// <summary>
/// Two-level caching decorator over ITenderService.
/// L1 = IMemoryCache (fast, per-instance, short TTL)
/// L2 = IDistributedCache (Redis via Aspire, shared across replicas, longer TTL)
///
/// Cache-aside pattern:
///   1. Check L1 → hit: return immediately
///   2. Check L2 → hit: populate L1, return
///   3. Miss → call underlying service → populate L2 then L1 → return
///
/// Invalidation: synchronous with write operations (POST/PUT/DELETE).
/// </summary>
public sealed class TenderCacheService : ITenderService
{
    private readonly ITenderService _inner;
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly ILogger<TenderCacheService> _logger;
    private readonly CacheMetrics _metrics;

    // TTL constants — L1 must be shorter than L2 to avoid stale in-memory data surviving a Redis update.
    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan L2Ttl = TimeSpan.FromMinutes(10);

    // MemoryCacheEntryOptions reused for all tender entries
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

    public TenderCacheService(
        ITenderService inner,
        IMemoryCache l1,
        IDistributedCache l2,
        ILogger<TenderCacheService> logger,
        CacheMetrics metrics)
    {
        _inner   = inner;
        _l1      = l1;
        _l2      = l2;
        _logger  = logger;
        _metrics = metrics;
    }

    // ── ProcuringEntity (rarely changes — good caching candidate) ───────────────

    public async Task<ProcuringEntityDto?> GetProcuringEntityByIdAsync(long id, CancellationToken ct = default)
    {
        var key = $"tenders:entity:{id}";
        return await GetOrCreateAsync(key, () => _inner.GetProcuringEntityByIdAsync(id, ct), ct);
    }

    public Task<IReadOnlyList<ProcuringEntityDto>> GetAllProcuringEntitiesAsync(CancellationToken ct = default)
    {
        // Reference data: low cardinality, rarely changes — cache the whole list.
        const string key = "tenders:entities:all";
        return GetOrCreateAsync<IReadOnlyList<ProcuringEntityDto>>(
            key, () => _inner.GetAllProcuringEntitiesAsync(ct)!, ct)!;
    }

    public async Task<ProcuringEntityDto> CreateProcuringEntityAsync(CreateProcuringEntityDto dto, CancellationToken ct = default)
    {
        var result = await _inner.CreateProcuringEntityAsync(dto, ct);
        // Write: invalidate the list cache
        Invalidate("tenders:entities:all", "EntityCreated");
        return result;
    }

    // ── Tender ───────────────────────────────────────────────────────────────────

    public async Task<TenderWithDetailsDto?> GetTenderByIdAsync(long id, CancellationToken ct = default)
    {
        var key = $"tenders:tender:{id}";
        return await GetOrCreateAsync(key, () => _inner.GetTenderByIdAsync(id, ct), ct);
    }

    public Task<IReadOnlyList<TenderDto>> GetTendersByEntityAsync(
        long procuringEntityId, string? status = null, CancellationToken ct = default)
    {
        // Key includes status so different filter combos are cached separately.
        var key = $"tenders:by-entity:{procuringEntityId}:{status ?? "all"}";
        return GetOrCreateAsync<IReadOnlyList<TenderDto>>(
            key, () => _inner.GetTendersByEntityAsync(procuringEntityId, status, ct)!, ct)!;
    }

    public async Task<TenderWithDetailsDto> CreateTenderAsync(CreateTenderDto dto, CancellationToken ct = default)
    {
        var result = await _inner.CreateTenderAsync(dto, ct);
        // Invalidate entity list cache (new tender visible in by-entity list)
        Invalidate($"tenders:by-entity:{dto.ProcuringEntityId}:all", "TenderCreated");
        return result;
    }

    public async Task ChangeTenderStatusAsync(
        long tenderId, string newStatus, string? reason = null, CancellationToken ct = default)
    {
        await _inner.ChangeTenderStatusAsync(tenderId, newStatus, reason, ct);
        // Invalidate specific tender and all by-entity caches (status changed)
        Invalidate($"tenders:tender:{tenderId}", "TenderStatusChanged");
    }

    public Task<IReadOnlyList<TenderStatusHistoryDto>> GetStatusHistoryAsync(long tenderId, CancellationToken ct = default)
        => _inner.GetStatusHistoryAsync(tenderId, ct);

    // ── Two-level cache helper ────────────────────────────────────────────────────

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
                _l1.Set(key, value, L1Options);   // back-populate L1 with shorter TTL
                _logger.LogDebug("Cache L2 hit {Key}", key);
                _metrics.RecordHit("L2");
                return value;
            }
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Cache is optional. The inner service reads ProcurementBidding.
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for {Key}", key);
        }

        _metrics.RecordMiss("L2");

        // DB / source call
        value = await factory();
        if (value is null) return default;

        // Populate L2 then L1
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

    private void Invalidate(string key, string reason)
    {
        _l1.Remove(key);
        _ = RemoveQuietlyAsync(key);
        _logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
        _metrics.RecordInvalidation(reason);
    }

    private async Task RemoveQuietlyAsync(string key)
    {
        try
        {
            await _l2.RemoveAsync(key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis unavailable, value for {Key} not invalidated", key);
        }
    }
}
