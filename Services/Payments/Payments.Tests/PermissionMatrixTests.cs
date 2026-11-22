using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Platform.ServiceDefaults.Auth;

namespace Payments.Tests;

/// <summary>
/// Locks the Lab 09 matrix: manager may update a tender and must be forbidden from deleting it
/// and from every payments endpoint. Missing permissions fail closed (the HTTP layer maps that to 403).
/// </summary>
public class PermissionMatrixTests
{
    [Fact]
    public async Task Manager_can_update_and_approve_orders_but_not_delete_or_touch_payments()
    {
        var auth = Build();
        var manager = Realm("manager");

        Assert.True((await auth.AuthorizeAsync(manager, "orders:read")).Succeeded);
        Assert.True((await auth.AuthorizeAsync(manager, "orders:update")).Succeeded);
        Assert.True((await auth.AuthorizeAsync(manager, "orders:approve")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(manager, "orders:delete")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(manager, "orders:create")).Succeeded);
        Assert.True((await auth.AuthorizeAsync(manager, "products:publish")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(manager, "payments:read")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(manager, "payments:process")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(manager, "payments:refund")).Succeeded);
    }

    [Fact]
    public async Task Authorization_claim_from_the_handout_allows_update_and_rejects_delete()
    {
        var auth = Build();
        const string claim = """
            {
              "permissions": [
                { "rsname": "orders-api", "scopes": ["orders:read", "orders:update", "orders:approve"] },
                { "rsname": "products-api", "scopes": ["products:read", "products:update", "products:publish"] }
              ]
            }
            """;
        var john = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("authorization", claim), new Claim("preferred_username", "john.manager")],
            authenticationType: "Test"));

        Assert.True((await auth.AuthorizeAsync(john, "orders:update")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(john, "orders:delete")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(john, "payments:read")).Succeeded);
    }

    [Theory]
    [InlineData("admin", "payments:refund", true)]
    [InlineData("user", "orders:create", true)]
    [InlineData("user", "orders:update", false)]
    [InlineData("user", "payments:process", false)]
    [InlineData("guest", "products:read", true)]
    [InlineData("guest", "orders:read", false)]
    [InlineData("guest", "products:create", false)]
    public async Task Role_matrix_matches_the_handout(string role, string permission, bool allowed)
    {
        var auth = Build();
        var result = await auth.AuthorizeAsync(Realm(role), permission);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task Malformed_authorization_claim_does_not_throw()
    {
        var auth = Build();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("authorization", "{not-json")],
            authenticationType: "Test"));

        var result = await auth.AuthorizeAsync(user, "orders:read");
        Assert.False(result.Succeeded);
    }

    private static IAuthorizationService Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPermissionAndScopePolicies(
            permissions:
            [
                "orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve",
                "products:read", "products:create", "products:update", "products:delete", "products:publish",
                "payments:read", "payments:process", "payments:refund"
            ],
            scopes: ["orders.read", "orders.write"]);
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Realm(string role)
    {
        var json = JsonSerializer.Serialize(new { roles = new[] { role } });
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("realm_access", json)],
            authenticationType: "Test"));
    }
}
