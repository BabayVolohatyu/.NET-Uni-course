using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// Three JWT issuers share one API. The policy scheme picks a handler from the
/// unverified <c>iss</c> claim; the chosen handler still validates the signature.
/// </summary>
public static class ProcurementAuthSchemes
{
    public const string Multi = "MultiAuth";

    /// <summary>Auth Service (ASP.NET Identity). Symmetric HMAC, issuer <c>procurement-auth</c>.</summary>
    public const string Local = "LocalJwt";

    /// <summary>Duende IdentityServer. Authority <c>http://localhost:5100</c>.</summary>
    public const string IdentityServer = "IdentityServer";

    /// <summary>
    /// Keycloak. <c>AddKeycloakJwtBearer</c> registers this as the framework default
    /// bearer scheme name ("Bearer").
    /// </summary>
    public const string Keycloak = "Bearer";

    public static string Select(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Local;

        var token = header["Bearer ".Length..].Trim();
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return Local;

        var issuer = handler.ReadJwtToken(token).Issuer ?? string.Empty;
        if (issuer.Contains("/realms/", StringComparison.OrdinalIgnoreCase))
            return Keycloak;

        if (issuer.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return IdentityServer;

        return Local;
    }
}
