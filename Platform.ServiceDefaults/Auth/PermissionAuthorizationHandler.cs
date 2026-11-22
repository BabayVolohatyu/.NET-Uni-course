using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// Reads <c>authorization.permissions</c> from the Keycloak access token and allows the
/// request only when the required scope is present. A missing or malformed claim does not
/// throw — the requirement fails and the middleware returns 403, not 500.
/// If the token has no authorization claim, realm roles are checked against the same matrix.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var authClaim = context.User.FindFirst("authorization")?.Value;
        if (authClaim is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(authClaim);
                var permissions = document.RootElement.GetProperty("permissions").EnumerateArray()
                    .SelectMany(p => p.GetProperty("scopes").EnumerateArray()
                        .Select(s => s.GetString()))
                    .OfType<string>()
                    .ToArray();

                if (permissions.Contains(requirement.Permission, StringComparer.Ordinal))
                    context.Succeed(requirement);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or JsonException)
            {
                // Fail closed. Do not turn a bad claim into HTTP 500.
            }

            return Task.CompletedTask;
        }

        if (RealmRolePermissions.Grants(context.User, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
