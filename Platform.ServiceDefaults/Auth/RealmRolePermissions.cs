using System.Security.Claims;
using System.Text.Json;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// Local copy of the Keycloak permission matrix (table 7). Payments are admin-only on purpose:
/// disbursement of reconstruction funds is the most sensitive operation in the platform.
/// Used when the access token carries realm roles but not yet an <c>authorization.permissions</c> claim.
/// </summary>
public static class RealmRolePermissions
{
    private static readonly Dictionary<string, string[]> Matrix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] =
        [
            "orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve",
            "products:read", "products:create", "products:update", "products:delete", "products:publish",
            "payments:read", "payments:process", "payments:refund"
        ],
        ["manager"] =
        [
            "orders:read", "orders:update", "orders:approve",
            "products:read", "products:update", "products:publish"
        ],
        ["user"] =
        [
            "orders:read", "orders:create",
            "products:read"
        ],
        ["guest"] =
        [
            "products:read"
        ]
    };

    public static bool Grants(ClaimsPrincipal user, string permission)
    {
        foreach (var role in ReadRoles(user))
        {
            if (Matrix.TryGetValue(role, out var granted) && granted.Contains(permission, StringComparer.Ordinal))
                return true;
        }

        return false;
    }

    public static IReadOnlyCollection<string> ReadRoles(ClaimsPrincipal user)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in user.FindAll(ClaimTypes.Role))
            roles.Add(claim.Value);
        foreach (var claim in user.FindAll("role"))
            roles.Add(claim.Value);
        foreach (var claim in user.FindAll("roles"))
            roles.Add(claim.Value);

        var realmAccess = user.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess))
        {
            try
            {
                using var document = JsonDocument.Parse(realmAccess);
                if (document.RootElement.TryGetProperty("roles", out var array))
                {
                    foreach (var role in array.EnumerateArray())
                    {
                        if (role.GetString() is { } name)
                            roles.Add(name);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore a malformed realm_access claim; the caller fails closed.
            }
        }

        return roles;
    }
}
