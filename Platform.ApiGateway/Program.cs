using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Platform.ServiceDefaults.Auth;
using Yarp.ReverseProxy.Transforms;

// ── API Gateway — single entry point for all clients ──────────────────────────
// Routes all requests to microservices via YARP reverse proxy.
// Lab 09: validates JWT before proxying, forwards Authorization, and adds X-User-Id.

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProcurementJwt(
    builder.Configuration,
    identityServerAudience: "orders_api",
    validateIdentityServerAudience: false);

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "DevRealmDrivers", options =>
    {
        // The gateway accepts tokens for every resource server and forwards them.
        // Each microservice checks its own audience and permission.
        options.TokenValidationParameters.ValidateAudience = false;
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(transforms =>
    {
        transforms.AddRequestTransform(context =>
        {
            // Do not trust a client-supplied user id. The gateway sets it from the validated token.
            context.ProxyRequest.Headers.Remove("X-User-Id");
            var user = context.HttpContext.User;
            var userId = user.FindFirst("userId")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
                context.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);

            // Authorization is forwarded by YARP with the rest of the request headers
            // so resource servers validate the same token independently.
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (IsAnonymous(context.Request.Path))
    {
        await next(context);
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Unauthorized",
            status = StatusCodes.Status401Unauthorized,
            detail = "A bearer token is required at the API gateway."
        });
        return;
    }

    await next(context);
});

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next(context);
    sw.Stop();

    var route = context.GetReverseProxyFeature()?.Route.Config.RouteId ?? "unknown";
    app.Logger.LogInformation(
        "Gateway routed {Method} {Path} → {Route}: {StatusCode} in {ElapsedMs} ms CorrelationId={CorrelationId}",
        context.Request.Method,
        context.Request.Path,
        route,
        context.Response.StatusCode,
        sw.ElapsedMilliseconds,
        context.Request.Headers["X-Correlation-Id"].ToString());
});

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();

static bool IsAnonymous(PathString path)
{
    var value = path.Value ?? string.Empty;
    if (value.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/alive", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        return true;

    // Client-credentials demo does not carry a user token.
    if (value.StartsWith("/api/aggregator/service-identity", StringComparison.OrdinalIgnoreCase))
        return true;

    var anonymousAuth = new[]
    {
        "/auth/register",
        "/auth/login",
        "/auth/refresh-token",
        "/auth/verify-email",
        "/auth/resend-verification",
        "/auth/forgot-password",
        "/auth/reset-password",
        "/auth/dev/mailbox"
    };

    return anonymousAuth.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
