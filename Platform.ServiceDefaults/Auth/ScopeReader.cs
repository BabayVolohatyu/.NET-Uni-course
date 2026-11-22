using System.Security.Claims;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// IdentityServer puts scopes in one space-delimited <c>scope</c> claim or in several claims.
/// </summary>
public static class ScopeReader
{
    public static bool HasScope(ClaimsPrincipal user, string scope)
    {
        foreach (var claim in user.FindAll("scope"))
        {
            foreach (var part in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(part, scope, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
}
