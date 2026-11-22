using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.WebApi.ExceptionHandling;

/// <summary>
/// Maps domain and MongoDB exceptions to ProblemDetails (RFC 9457).
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var status = MapStatus(ex);
        logger.LogError(ex, "Request {Method} {Path} failed with HTTP {Status}",
            http.Request.Method, http.Request.Path, status);

        http.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = Title(status),
            Detail = Detail(ex, status, environment.IsDevelopment()),
            Type = $"https://httpstatuses.io/{status}"
        };

        if (ex is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                .ToArray();
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = problem
        });
    }

    private static int MapStatus(Exception ex) => ex switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ValidationException => StatusCodes.Status400BadRequest,
        ConflictException => StatusCodes.Status409Conflict,
        DomainException => StatusCodes.Status400BadRequest,
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } => StatusCodes.Status409Conflict,
        MongoBulkWriteException bulk when bulk.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey)
            => StatusCodes.Status409Conflict,
        MongoWriteException => StatusCodes.Status400BadRequest,
        MongoConnectionException => StatusCodes.Status503ServiceUnavailable,
        TimeoutException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => "Internal Server Error"
    };

    private static string Detail(Exception ex, int status, bool isDevelopment)
    {
        if (status == StatusCodes.Status503ServiceUnavailable)
            return "MongoDB is unavailable.";

        if (IsDuplicateKey(ex))
            return "A document with the same unique key already exists.";

        if (ex is MongoWriteException)
            return "The document could not be written.";

        if (isDevelopment || ex is NotFoundException or ConflictException or DomainException or ValidationException)
            return ex.Message;

        return "An unexpected error occurred. Check server logs for details.";
    }

    private static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } => true,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        _ => false
    };
}
