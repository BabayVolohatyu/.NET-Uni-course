using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tenders.Domain.Exceptions;

namespace Tenders.Api.Middleware;

/// <summary>
/// Centralized exception handler: converts domain exceptions to ProblemDetails responses (RFC 9457).
/// Registered once in Program.cs — no try/catch in individual controllers.
///
/// Exception → HTTP status mapping:
///   NotFoundException          → 404 Not Found
///   BusinessConflictException  → 409 Conflict     (invalid status transition, duplicate, etc.)
///   ValidationException        → 400 Bad Request
///   Everything else            → 500 Internal Server Error (message is hidden from client)
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        http.Response.StatusCode = ex switch
        {
            NotFoundException        => StatusCodes.Status404NotFound,
            BusinessConflictException => StatusCodes.Status409Conflict,
            ValidationException       => StatusCodes.Status400BadRequest,
            _                         => StatusCodes.Status500InternalServerError
        };

        return await problemDetails.TryWriteAsync(new()
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Title  = ex switch
                {
                    NotFoundException         => "Not Found",
                    BusinessConflictException => "Business Conflict",
                    ValidationException       => "Validation Error",
                    _                         => "Internal Server Error"
                },
                Detail = ex is NotFoundException or BusinessConflictException or ValidationException
                    ? ex.Message
                    : "An unexpected error occurred. Check server logs for details."
            }
        });
    }
}
