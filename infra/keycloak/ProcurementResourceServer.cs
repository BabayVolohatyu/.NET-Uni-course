using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace Procurement.Keycloak;

/// <summary>
/// One Keycloak resource server: authorization scopes, one protected resource,
/// a role policy per realm role, and a scope permission that binds them.
/// A role with an empty scope list (payments for manager, user and guest) gets nothing.
/// </summary>
internal static class ProcurementResourceServer
{
    public static Client Create(
        string resourceName,
        string clientId,
        Input<string> realmId,
        string secret,
        IReadOnlyDictionary<string, string[]> scopesByRole,
        IReadOnlyDictionary<string, Role> roles,
        CustomResourceOptions options)
    {
        var client = OpenIdClientFactory.ResourceServer(resourceName, clientId, realmId, secret, options);

        var scopeIds = new Dictionary<string, Output<string>>(StringComparer.Ordinal);
        foreach (var scope in scopesByRole.Values.SelectMany(v => v).Distinct(StringComparer.Ordinal))
        {
            var created = new ClientAuthorizationScope($"{resourceName}-{scope.Replace(':', '-')}", new()
            {
                RealmId = realmId,
                ResourceServerId = client.ResourceServerId,
                Name = scope,
                DisplayName = scope,
            }, options);
            scopeIds[scope] = created.Id;
        }

        _ = new ClientAuthorizationResource($"{resourceName}-resource", new()
        {
            RealmId = realmId,
            ResourceServerId = client.ResourceServerId,
            Name = clientId,
            DisplayName = clientId,
            Scopes = scopeIds.Keys.ToArray(),
        }, options);

        foreach (var (roleName, scopes) in scopesByRole)
        {
            if (scopes.Length == 0)
                continue;

            var policy = new ClientRolePolicy($"{resourceName}-{roleName}-policy", new()
            {
                RealmId = realmId,
                ResourceServerId = client.ResourceServerId,
                Name = $"{roleName}-on-{clientId}",
                Description = $"Realm role {roleName} may call the listed scopes on {clientId}.",
                DecisionStrategy = "UNANIMOUS",
                Logic = "POSITIVE",
                Type = "role",
                Roles = new[]
                {
                    new ClientRolePolicyRoleArgs
                    {
                        Id = roles[roleName].Id,
                        Required = true,
                    },
                },
            }, options);

            _ = new ClientAuthorizationPermission($"{resourceName}-{roleName}-permission", new()
            {
                RealmId = realmId,
                ResourceServerId = client.ResourceServerId,
                Name = $"{roleName}-scopes-on-{clientId}",
                Description = $"Grants {string.Join(", ", scopes)} to {roleName}.",
                DecisionStrategy = "AFFIRMATIVE",
                Type = "scope",
                Policies = new[] { policy.Id },
                Scopes = scopes.Select(scope => scopeIds[scope]).ToArray(),
            }, options);
        }

        return client;
    }
}
