using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RiskAssessments.Application.Common.Behaviors;

/// <summary>
/// Structured request log. Events are written by Serilog in the WebApi host.
/// A validation failure is logged as "handler was not invoked" — the handler never runs.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", name, request);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName}: handler completed", name);
            return response;
        }
        catch (ValidationException)
        {
            logger.LogInformation(
                "Validation failed for {RequestName}; handler was not invoked",
                name);
            throw;
        }
    }
}
