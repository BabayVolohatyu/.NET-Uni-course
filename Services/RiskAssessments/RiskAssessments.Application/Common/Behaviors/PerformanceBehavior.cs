using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RiskAssessments.Application.Common.Behaviors;

/// <summary>Warns when a handler (and its MongoDB calls) takes longer than 500 ms.</summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestMilliseconds = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await next();
        }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds > SlowRequestMilliseconds)
            {
                logger.LogWarning(
                    "Slow request {Request}: {Elapsed} ms",
                    typeof(TRequest).Name,
                    sw.ElapsedMilliseconds);
            }
        }
    }
}
