using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Platform.IdentityServer;

/// <summary>
/// OAuth clients, API resources, and scopes for the procurement platform.
/// orders_api is Tenders (the handout's orders service). products_api is Catalog (contractors).
/// </summary>
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("orders.read", "Read reconstruction tenders"),
        new ApiScope("orders.write", "Create and update reconstruction tenders"),
        new ApiScope("products.read", "Read the contractor registry"),
        new ApiScope("products.write", "Update contractor qualifications"),
        new ApiScope("orders_api", "Tenders resource"),
        new ApiScope("products_api", "Contractor catalog resource")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("orders_api", "Tenders / orders API")
        {
            Scopes = { "orders.read", "orders.write", "orders_api" }
        },
        new ApiResource("products_api", "Contractor catalog / products API")
        {
            Scopes = { "products.read", "products.write", "products_api" }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Postman — Authorization Code
        new Client
        {
            ClientId = "postman",
            ClientName = "Postman — auditor authorization code",
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets = { new Secret("secret".Sha256()) },
            RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            PostLogoutRedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "products_api",
                "orders.read", "orders.write",
                "products.read", "products.write"
            },
            AllowOfflineAccess = true,
            RequirePkce = false,
            AllowAccessTokensViaBrowser = true
        },
        // Aggregator — Client Credentials (service-to-service, no user)
        new Client
        {
            ClientId = "aggregator_service",
            ClientName = "Aggregator — client credentials",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("aggregator_secret".Sha256()) },
            AllowedScopes =
            {
                "orders_api", "products_api",
                "orders.read", "orders.write",
                "products.read", "products.write"
            }
        },
        // Swagger UI — public client with PKCE, no secret
        new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI — public PKCE client",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris =
            {
                "https://localhost:5001/swagger/oauth2-redirect.html",
                "http://localhost:5052/swagger/oauth2-redirect.html",
                "http://localhost:5002/swagger/oauth2-redirect.html"
            },
            AllowedCorsOrigins =
            {
                "http://localhost:5052",
                "http://localhost:5002"
            },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api",
                "orders.read", "orders.write"
            },
            AllowAccessTokensViaBrowser = true
        }
    ];
}
