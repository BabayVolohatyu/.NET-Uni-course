using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace Procurement.Keycloak;

/// <summary>
/// Audience, default scopes and the permissions claim are the same for every login client,
/// so they live here instead of being copied into each client declaration.
/// </summary>
internal static class ClientMappingExtensions
{
    /// <summary>
    /// Keycloak script that copies table 7 into the <c>authorization</c> claim.
    /// Payments scopes are emitted only for the admin realm role.
    /// </summary>
    private const string PermissionsScript = """
        var roles = {};
        var iterator = user.getRealmRoleMappingsStream().iterator();
        while (iterator.hasNext()) {
          roles[iterator.next().getName()] = true;
        }
        function has(name) { return roles[name] === true; }
        var permissions = [];
        function grant(rsname, pairs) {
          var scopes = [];
          for (var i = 0; i < pairs.length; i++) {
            if (pairs[i][0]) scopes.push(pairs[i][1]);
          }
          if (scopes.length > 0) permissions.push({ rsname: rsname, scopes: scopes });
        }
        var admin = has('admin');
        var manager = has('manager');
        var clerk = has('user');
        var guest = has('guest');
        grant('orders-api', [
          [admin || manager || clerk, 'orders:read'],
          [admin || clerk, 'orders:create'],
          [admin || manager, 'orders:update'],
          [admin, 'orders:delete'],
          [admin || manager, 'orders:approve']
        ]);
        grant('products-api', [
          [admin || manager || clerk || guest, 'products:read'],
          [admin, 'products:create'],
          [admin || manager, 'products:update'],
          [admin, 'products:delete'],
          [admin || manager, 'products:publish']
        ]);
        grant('payments-api', [
          [admin, 'payments:read'],
          [admin, 'payments:process'],
          [admin, 'payments:refund']
        ]);
        exports = { permissions: permissions };
        """;

    public static Client WithAudienceMapper(
        this Client client, Input<string> realmId, CustomResourceOptions options, params string[] audiences)
    {
        foreach (var audience in audiences)
        {
            _ = new AudienceProtocolMapper($"{client.GetResourceName()}-aud-{audience}", new()
            {
                RealmId = realmId,
                ClientId = client.Id,
                Name = $"audience-{audience}",
                IncludedCustomAudience = audience,
                AddToAccessToken = true,
                AddToIdToken = false,
            }, options);
        }

        return client;
    }

    public static Client WithDefaultScopes(
        this Client client, Input<string> realmId, CustomResourceOptions options, params string[] scopes)
    {
        _ = new ClientDefaultScopes($"{client.GetResourceName()}-default-scopes", new()
        {
            RealmId = realmId,
            ClientId = client.Id,
            DefaultScopes = scopes,
        }, options);
        return client;
    }

    /// <summary>
    /// Puts <c>authorization.permissions</c> on the access token so each API can check a scope
    /// without calling Keycloak again.
    /// </summary>
    public static Client WithPermissionsProtocolMapper(
        this Client client, Input<string> realmId, CustomResourceOptions options)
    {
        _ = new ScriptProtocolMapper($"{client.GetResourceName()}-permissions", new()
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = "authorization-permissions",
            ClaimName = "authorization",
            ClaimValueType = "JSON",
            AddToAccessToken = true,
            AddToIdToken = false,
            AddToUserinfo = false,
            Multivalued = false,
            Script = PermissionsScript,
        }, options);
        return client;
    }
}
