using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults.Auth;

public static class ProcurementAuthenticationExtensions
{
    /// <summary>
    /// Symmetric JWT from the Auth Service plus IdentityServer access tokens (Authority + Audience).
    /// Keycloak is added beside this call with <c>AddKeycloakJwtBearer</c> so the realm stays visible
    /// in each service's <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddProcurementJwt(
        this IServiceCollection services,
        IConfiguration configuration,
        string identityServerAudience,
        bool validateIdentityServerAudience = true)
    {
        var jwt = configuration.GetSection("Jwt");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ProcurementAuthSchemes.Multi;
                options.DefaultChallengeScheme = ProcurementAuthSchemes.Multi;
            })
            .AddPolicyScheme(ProcurementAuthSchemes.Multi, "JWT issuer router", options =>
            {
                options.ForwardDefaultSelector = ProcurementAuthSchemes.Select;
            })
            .AddJwtBearer(ProcurementAuthSchemes.Local, options =>
            {
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt["SecretKey"]!)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };
            })
            .AddJwtBearer(ProcurementAuthSchemes.IdentityServer, options =>
            {
                options.Authority = configuration["IdentityServer:Authority"] ?? "http://localhost:5100";
                options.Audience = identityServerAudience;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters.ValidateAudience = validateIdentityServerAudience;
                options.TokenValidationParameters.ValidAudience = identityServerAudience;
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.RoleClaimType = "role";
            });

        return services;
    }

    /// <summary>
    /// Scope policies (<c>orders.read</c>) for IdentityServer and permission policies
    /// (<c>orders:create</c>) for Keycloak. One handler serves every permission policy.
    /// </summary>
    public static IServiceCollection AddPermissionAndScopePolicies(
        this IServiceCollection services,
        IEnumerable<string> permissions,
        IEnumerable<string> scopes)
    {
        services.AddAuthorization(options =>
        {
            foreach (var scope in scopes)
            {
                var required = scope;
                options.AddPolicy(required, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(ctx => ScopeReader.HasScope(ctx.User, required));
                });
            }

            foreach (var permission in permissions)
            {
                var required = permission;
                options.AddPolicy(required, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new PermissionRequirement(required));
                });
            }
        });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
