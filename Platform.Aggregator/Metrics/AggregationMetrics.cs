using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Metrics;

/// <summary>
/// Business metrics for dossier assembly.
/// result is only "complete" or "partial" — never a tender id.
/// </summary>
public sealed class AggregationMetrics
{
    public const string MeterName = "Platform.Aggregator";

    private readonly Counter<long> _responses;
    private readonly Histogram<double> _operationDuration;
    private int _inFlight;

    public AggregationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _responses = meter.CreateCounter<long>(
            "aggregator.dossiers",
            unit: "{dossier}",
            description: "Full tender dossiers assembled by the Aggregator.");
        _operationDuration = meter.CreateHistogram<double>(
            "aggregator.operation.duration",
            unit: "ms",
            description: "Duration of dossier assembly, including partial and failed attempts.");

        meter.CreateObservableGauge(
            "aggregator.operations.inflight",
            () => Volatile.Read(ref _inFlight),
            unit: "{operation}",
            description: "Dossier assemblies currently in flight.");
    }

    public void DossierAssembled(bool partial) =>
        _responses.Add(1, new KeyValuePair<string, object?>(
            "result", partial ? "partial" : "complete"));

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

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _operationDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
