using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Caching;

/// <summary>
/// Custom OpenTelemetry metrics for the Platform Aggregator cache.
/// Tracks hits and misses for aggregated responses in Redis.
///
/// Registered in ServiceDefaults via .WithMetrics(m => m.AddMeter("Platform.Aggregator.Cache")).
/// </summary>
public sealed class AggregatorCacheMetrics
{
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public const string MeterName = "Platform.Aggregator.Cache";

    public AggregatorCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>(
            "cache.hits",
            description: "Number of aggregated response cache hits.");

        _misses = meter.CreateCounter<long>(
            "cache.misses",
            description: "Number of aggregated response cache misses.");
    }

    public void RecordHit(string key) =>
        _hits.Add(1, new KeyValuePair<string, object?>("aggregator.cache.key_prefix", key));

    public void RecordMiss(string key) =>
        _misses.Add(1, new KeyValuePair<string, object?>("aggregator.cache.key_prefix", key));
}
