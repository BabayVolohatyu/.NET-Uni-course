using System.Diagnostics.Metrics;

namespace RiskAssessments.Application.Caching;

/// <summary>
/// Custom OpenTelemetry metrics for the RiskAssessments service cache.
/// Tracks hits and misses for both L1 (in-memory) and L2 (Redis) cache levels.
///
/// Registered in ServiceDefaults via .WithMetrics(m => m.AddMeter("RiskAssessments.Cache")).
/// Visible as cache.hits / cache.misses counters in Aspire Dashboard → Metrics.
/// </summary>
public sealed class CacheMetrics
{
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    // The meter name must match the name registered in ServiceDefaults AddMeter call.
    public const string MeterName = "RiskAssessments.Cache";

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>(
            "cache.hits",
            description: "Number of cache hits in the RiskAssessments service.");

        _misses = meter.CreateCounter<long>(
            "cache.misses",
            description: "Number of cache misses in the RiskAssessments service.");

        _invalidations = meter.CreateCounter<long>(
            "cache.invalidations",
            description: "Number of cache entries invalidated in the RiskAssessments service.");
    }

    /// <summary>Record a cache hit at the given level ("L1" or "L2").</summary>
    public void RecordHit(string level) =>
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    /// <summary>Record a cache miss at the given level ("L1", "L2", or "DB").</summary>
    public void RecordMiss(string level) =>
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    /// <summary>Record a cache invalidation with the reason.</summary>
    public void RecordInvalidation(string reason) =>
        _invalidations.Add(1, new KeyValuePair<string, object?>("cache.invalidation_reason", reason));
}
