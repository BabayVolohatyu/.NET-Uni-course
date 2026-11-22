using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Tenders.Api.Metrics;

/// <summary>
/// Business metrics for the procurement-bidding service.
/// Registered as a singleton. The meter name "Tenders.Api" is added in ServiceDefaults.
///
/// Tag cardinality is bounded on purpose:
///   tender.status — Draft, Published, BiddingOpen, EvaluationInProgress, Awarded, Cancelled, Other
///   operation     — get, create, status_change, submit_bid, award
///   status        — success, failure
/// Tender ids, contractor ids, and user ids are never tags.
/// </summary>
public sealed class TenderMetrics
{
    public const string MeterName = "Tenders.Api";

    private static readonly string[] KnownStatuses =
    [
        "Draft", "Published", "BiddingOpen", "EvaluationInProgress", "Awarded", "Cancelled"
    ];

    private readonly Counter<long> _created;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _bidsSubmitted;
    private readonly Counter<long> _awarded;
    private readonly Histogram<double> _operationDuration;

    private int _awaitingPublication;
    private int _inFlight;

    public TenderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>(
            "tenders.created",
            unit: "{tender}",
            description: "Tenders created in the procurement-bidding service.");
        _statusChanged = meter.CreateCounter<long>(
            "tenders.status_changed",
            unit: "{transition}",
            description: "Tender lifecycle transitions accepted by the service.");
        _bidsSubmitted = meter.CreateCounter<long>(
            "bids.submitted",
            unit: "{bid}",
            description: "Bids submitted against an open tender.");
        _awarded = meter.CreateCounter<long>(
            "tenders.awarded",
            unit: "{tender}",
            description: "Tenders awarded to a winning contractor.");
        _operationDuration = meter.CreateHistogram<double>(
            "tenders.operation.duration",
            unit: "ms",
            description: "Duration of tender operations, including failures.");

        meter.CreateObservableGauge(
            "tenders.awaiting_publication",
            () => Volatile.Read(ref _awaitingPublication),
            unit: "{tender}",
            description: "Draft tenders created in this process that have not yet left Draft.");
        meter.CreateObservableGauge(
            "tenders.operations.inflight",
            () => Volatile.Read(ref _inFlight),
            unit: "{operation}",
            description: "Tender operations currently executing in this process.");
    }

    public void TenderCreated(string status)
    {
        var normalized = NormalizeStatus(status);
        _created.Add(1, new KeyValuePair<string, object?>("tender.status", normalized));
        if (normalized == "Draft")
            Interlocked.Increment(ref _awaitingPublication);
    }

    public void TenderStatusChanged(string status)
    {
        var normalized = NormalizeStatus(status);
        _statusChanged.Add(1, new KeyValuePair<string, object?>("tender.status", normalized));
        if (normalized != "Draft")
            DecrementFloor(ref _awaitingPublication);
    }

    public void BidSubmitted() =>
        _bidsSubmitted.Add(1, new KeyValuePair<string, object?>("bid.result", "submitted"));

    public void TenderAwarded() =>
        _awarded.Add(1, new KeyValuePair<string, object?>("tender.status", "Awarded"));

    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _inFlight);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            RecordDuration(operation, success: true, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch
        {
            RecordDuration(operation, success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action) =>
        MeasureAsync(operation, async () =>
        {
            await action();
            return true;
        });

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _operationDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    public static string NormalizeStatus(string? status)
    {
        if (status is null)
            return "Other";
        foreach (var known in KnownStatuses)
        {
            if (string.Equals(known, status, StringComparison.OrdinalIgnoreCase))
                return known;
        }
        return "Other";
    }

    private static void DecrementFloor(ref int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref value);
            if (current <= 0)
                return;
        }
        while (Interlocked.CompareExchange(ref value, current - 1, current) != current);
    }
}
