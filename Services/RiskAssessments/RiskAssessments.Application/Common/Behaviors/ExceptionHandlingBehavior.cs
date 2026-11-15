using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RiskAssessments.Application.Common.Behaviors;

/// <summary>
/// Logs exceptions at the MediatR boundary and rethrows them.
/// HTTP status mapping stays in the WebApi <c>IExceptionHandler</c>.
/// Validation failures are not logged here — <see cref="LoggingBehavior{TRequest,TResponse}"/> records them.
/// </summary>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            logger.LogError(ex, "Request {RequestName} failed", typeof(TRequest).Name);
            throw;
        }
    }
}
