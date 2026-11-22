using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RiskAssessments.WebApi.Metrics;

/// <summary>
/// Business metrics for audit verdicts.
/// Meter name "RiskAssessments.Api" matches the logical service, not the project suffix.
/// risk.level is a closed set (Low, Medium, High, Critical, Other). Auditor ids are not tags.
/// </summary>
public sealed class RiskAssessmentMetrics
{
    public const string MeterName = "RiskAssessments.Api";

    private static readonly string[] KnownLevels = ["Low", "Medium", "High", "Critical"];

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Histogram<double> _operationDuration;
    private int _open;
    private int _inFlight;

    public RiskAssessmentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>(
            "risk_assessments.created",
            unit: "{assessment}",
            description: "Risk assessments recorded for reconstruction tenders.");
        _updated = meter.CreateCounter<long>(
            "risk_assessments.updated",
            unit: "{assessment}",
            description: "Risk assessments updated, flagged, or withdrawn.");
        _operationDuration = meter.CreateHistogram<double>(
            "risk_assessments.operation.duration",
            unit: "ms",
            description: "Duration of risk-assessment operations, including failures.");

        meter.CreateObservableGauge(
            "risk_assessments.open",
            () => Volatile.Read(ref _open),
            unit: "{assessment}",
            description: "Assessments created in this process that have not been deleted.");
        meter.CreateObservableGauge(
            "risk_assessments.operations.inflight",
            () => Volatile.Read(ref _inFlight),
            unit: "{operation}",
            description: "Risk-assessment operations currently executing in this process.");
    }

    public void AssessmentCreated(string? riskLevel)
    {
        _created.Add(1, new KeyValuePair<string, object?>("risk.level", NormalizeLevel(riskLevel)));
        Interlocked.Increment(ref _open);
    }

    public void AssessmentUpdated(string result) =>
        _updated.Add(1, new KeyValuePair<string, object?>("assessment.result", result));

    public void AssessmentDeleted()
    {
        _updated.Add(1, new KeyValuePair<string, object?>("assessment.result", "deleted"));
        DecrementFloor(ref _open);
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

    public static string NormalizeLevel(string? riskLevel)
    {
        if (riskLevel is null)
            return "Other";
        foreach (var known in KnownLevels)
        {
            if (string.Equals(known, riskLevel, StringComparison.OrdinalIgnoreCase))
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
