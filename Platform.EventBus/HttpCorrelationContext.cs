using Microsoft.AspNetCore.Http;

namespace Platform.EventBus;

/// <summary>
/// Reads the Lab 05 correlation id from the current request.
/// </summary>
public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";

    public string? Current
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
