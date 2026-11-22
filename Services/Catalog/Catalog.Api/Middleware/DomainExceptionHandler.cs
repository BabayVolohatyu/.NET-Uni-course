using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Middleware;

/// <summary>
/// Centralized exception handler: converts domain exceptions to ProblemDetails responses (RFC 9457).
/// Registered once in Program.cs — no try/catch scattered across individual controllers.
///
/// Exception → HTTP status mapping:
///   NotFoundException   → 404 Not Found
///   ConflictException   → 409 Conflict  (duplicate EDRPOU, etc.)
///   DomainException     → 409 Conflict  (invariant violation inside entity)
///   Everything else     → 500 Internal Server Error (message is hidden from client in Production)
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        // Log once — never re-logged in controllers
        logger.LogError(ex, "Request {Method} {Path} failed", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = ex switch
        {
            NotFoundException  => StatusCodes.Status404NotFound,
            ConflictException  => StatusCodes.Status409Conflict,
            DomainException    => StatusCodes.Status409Conflict,
            _                  => StatusCodes.Status500InternalServerError
        };

        var isDev = http.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        return await problemDetails.TryWriteAsync(new()
        {
            HttpContext = http,
            Exception   = ex,
            ProblemDetails = new ProblemDetails
            {
                Title = ex switch
                {
                    NotFoundException => "Not Found",
                    ConflictException => "Conflict",
                    DomainException   => "Domain Rule Violation",
                    _                 => "Internal Server Error"
                },
                // Expose detail message only in Development; hide in Production
                Detail = isDev || ex is NotFoundException or ConflictException or DomainException
                    ? ex.Message
                    : "An unexpected error occurred. Check server logs for details."
            }
        });
    }
}
