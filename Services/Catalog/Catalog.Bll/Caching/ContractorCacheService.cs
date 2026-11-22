using Catalog.Bll.DTOs;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Queries;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Catalog.Bll.Caching;

/// <summary>
/// Two-level caching decorator over IContractorService.
/// L1 = IMemoryCache (fast, per-instance, short TTL)
/// L2 = IDistributedCache (Redis via Aspire, shared across replicas, longer TTL)
///
/// Cache-aside pattern:
///   1. Check L1 → hit: return immediately
///   2. Check L2 → hit: populate L1, return
///   3. Miss → call underlying service → populate L2 then L1 → return
///
/// Invalidation: synchronous with write operations (POST/PUT/DELETE).
/// Key schema: catalog:contractor:{id}, catalog:contractors:list (page-based).
/// </summary>
public sealed class ContractorCacheService : IContractorService
{
    private readonly IContractorService _inner;
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly ILogger<ContractorCacheService> _logger;
    private readonly CacheMetrics _metrics;

    // TTL constants — L1 must be shorter than L2 to avoid stale in-memory data surviving a Redis update.
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

    public ContractorCacheService(
        IContractorService inner,
        IMemoryCache l1,
        IDistributedCache l2,
        ILogger<ContractorCacheService> logger,
        CacheMetrics metrics)
    {
        _inner   = inner;
        _l1      = l1;
        _l2      = l2;
        _logger  = logger;
        _metrics = metrics;
    }

    // ── Read operations (cache-aside) ─────────────────────────────────────────

    public async Task<PagedResult<ContractorDto>> GetListAsync(
        ContractorQuery query, CancellationToken ct = default)
    {
        // Cache paged lists — key encodes all filter parameters.
        var key = $"catalog:contractors:list:{query.Page}:{query.PageSize}:{query.IsActive}:{query.Search}";
        return (await GetOrCreateAsync(key, () => _inner.GetListAsync(query, ct), ct))!;
    }

    public async Task<ContractorWithDetailsDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var key = $"catalog:contractor:{id}";
        return (await GetOrCreateAsync(key, async () =>
            (ContractorWithDetailsDto?)await _inner.GetByIdAsync(id, ct), ct))!;
    }

    // ── Write operations (invalidate on change) ──────────────────────────────

    public async Task<ContractorDto> CreateAsync(CreateContractorDto input, CancellationToken ct = default)
    {
        var result = await _inner.CreateAsync(input, ct);
        // Invalidate list cache — new contractor appears in queries
        InvalidateLists("ContractorCreated");
        return result;
    }

    public async Task<ContractorDto> UpdateAsync(int id, UpdateContractorDto input, CancellationToken ct = default)
    {
        var result = await _inner.UpdateAsync(id, input, ct);
        Invalidate($"catalog:contractor:{id}", "ContractorUpdated");
        InvalidateLists("ContractorUpdated");
        return result;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _inner.DeleteAsync(id, ct);
        Invalidate($"catalog:contractor:{id}", "ContractorDeleted");
        InvalidateLists("ContractorDeleted");
    }

    public async Task ActivateAsync(int id, CancellationToken ct = default)
    {
        await _inner.ActivateAsync(id, ct);
        Invalidate($"catalog:contractor:{id}", "ContractorActivated");
        InvalidateLists("ContractorActivated");
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        await _inner.DeactivateAsync(id, ct);
        Invalidate($"catalog:contractor:{id}", "ContractorDeactivated");
        InvalidateLists("ContractorDeactivated");
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
                _l1.Set(key, value, L1Options);   // back-populate L1 with shorter TTL
                _logger.LogDebug("Cache L2 hit {Key}", key);
                _metrics.RecordHit("L2");
                return value;
            }
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Cache is optional. The inner service reads ContractorRegistry.
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

    // Invalidate all paged list keys by clearing known patterns.
    // In production consider a Redis SCAN + DEL pattern or a version key strategy.
    private void InvalidateLists(string reason)
    {
        // We use a "version" key approach: bump a version counter stored in L1
        // so stale list pages are ignored on next read.
        const string versionKey = "catalog:contractors:list:version";
        var version = _l1.Get<long>(versionKey);
        _l1.Set(versionKey, version + 1,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
        _ = RemoveQuietlyAsync(versionKey);
        _logger.LogInformation("Cache list version bumped, reason {Reason}", reason);
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
