using Pulumi;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace Procurement.Keycloak;

/// <summary>
/// Builds the three OAuth clients the lab asks for. Resource servers are confidential
/// clients with authorization services switched on — they are not login clients.
/// </summary>
internal static class OpenIdClientFactory
{
    public static Client AuthorizationCode(
        string name,
        string clientId,
        Input<string> realmId,
        bool publicClient,
        string? secret,
        CustomResourceOptions options,
        params string[] redirectUris)
        => new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Enabled = true,
            AccessType = publicClient ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            ImplicitFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = false,
            ClientSecret = publicClient ? null : secret,
            PkceCodeChallengeMethod = publicClient ? "S256" : null,
            ConsentRequired = true,
            ValidRedirectUris = redirectUris,
            WebOrigins = ["+"],
            FullScopeAllowed = true,
        }, options);

    public static Client ClientCredentials(
        string name,
        string clientId,
        Input<string> realmId,
        string secret,
        CustomResourceOptions options)
        => new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Description = "Aggregator service account. No human subject; cannot disburse funds.",
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = true,
            ClientSecret = secret,
            FullScopeAllowed = true,
        }, options);

    public static Client ResourceServer(
        string name,
        string clientId,
        Input<string> realmId,
        string secret,
        CustomResourceOptions options)
        => new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Description = $"Resource server for {clientId}. Permissions are evaluated locally by the API.",
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ClientSecret = secret,
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                DecisionStrategy = "AFFIRMATIVE",
                AllowRemoteResourceManagement = false,
                KeepDefaults = false,
            },
        }, options);
}
