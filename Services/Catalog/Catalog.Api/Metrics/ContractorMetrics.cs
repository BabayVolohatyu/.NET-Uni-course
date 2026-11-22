using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

/// <summary>
/// Business metrics for the contractor registry.
/// Meter name "Catalog.Api" is exported from ServiceDefaults.
/// Tags stay low-cardinality: contractor.result and operation/status. No EDRPOU, no ids.
/// </summary>
public sealed class ContractorMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _registered;
    private readonly Counter<long> _changed;
    private readonly Histogram<double> _operationDuration;
    private int _active;
    private int _inFlight;

    public ContractorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _registered = meter.CreateCounter<long>(
            "contractors.registered",
            unit: "{contractor}",
            description: "Contractors added to the reconstruction registry.");
        _changed = meter.CreateCounter<long>(
            "contractors.changed",
            unit: "{change}",
            description: "Registry mutations other than first registration.");
        _operationDuration = meter.CreateHistogram<double>(
            "contractors.operation.duration",
            unit: "ms",
            description: "Duration of contractor-registry operations, including failures.");

        meter.CreateObservableGauge(
            "contractors.active",
            () => Volatile.Read(ref _active),
            unit: "{contractor}",
            description: "Contractors registered in this process that have not been deactivated or removed.");
        meter.CreateObservableGauge(
            "contractors.operations.inflight",
            () => Volatile.Read(ref _inFlight),
            unit: "{operation}",
            description: "Contractor operations currently executing in this process.");
    }

    public void ContractorRegistered()
    {
        _registered.Add(1, new KeyValuePair<string, object?>("contractor.result", "registered"));
        Interlocked.Increment(ref _active);
    }

    public void ContractorChanged(string result)
    {
        _changed.Add(1, new KeyValuePair<string, object?>("contractor.result", result));
        if (result is "deactivated" or "removed")
            DecrementFloor(ref _active);
        else if (result == "activated")
            Interlocked.Increment(ref _active);
    }

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
