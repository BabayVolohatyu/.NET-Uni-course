using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Platform.IdentityServer;

public static class SeedData
{
    public static async Task EnsureSeedAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var application = provider.GetRequiredService<ApplicationDbContext>();
        var configuration = provider.GetRequiredService<ConfigurationDbContext>();
        var grants = provider.GetRequiredService<PersistedGrantDbContext>();

        await application.Database.MigrateAsync(ct);
        await configuration.Database.MigrateAsync(ct);
        await grants.Database.MigrateAsync(ct);

        await SeedRolesAndUsersAsync(provider, logger);
        await SeedConfigurationAsync(configuration, logger, ct);
    }

    private static async Task SeedRolesAndUsersAsync(IServiceProvider provider, ILogger logger)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "User", "Admin" })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(users, logger, "auditor.admin", "auditor.admin@procurement.audit", "Admin123!", "Admin", "User");
        await EnsureUserAsync(users, logger, "auditor.user", "auditor.user@procurement.audit", "User12345!", "User");
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> users,
        ILogger logger,
        string userName,
        string email,
        string password,
        params string[] roles)
    {
        if (await users.FindByNameAsync(userName) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = userName
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));

        await users.AddToRolesAsync(user, roles);
        logger.LogInformation("Seeded IdentityServer user {UserName}", userName);
    }

    private static async Task SeedConfigurationAsync(ConfigurationDbContext context, ILogger logger, CancellationToken ct)
    {
        foreach (var resource in Config.IdentityResources)
        {
            if (!await context.IdentityResources.AnyAsync(r => r.Name == resource.Name, ct))
                context.IdentityResources.Add(resource.ToEntity());
        }

        foreach (var scope in Config.ApiScopes)
        {
            if (!await context.ApiScopes.AnyAsync(s => s.Name == scope.Name, ct))
                context.ApiScopes.Add(scope.ToEntity());
        }

        foreach (var resource in Config.ApiResources)
        {
            if (!await context.ApiResources.AnyAsync(r => r.Name == resource.Name, ct))
                context.ApiResources.Add(resource.ToEntity());
        }

        foreach (var client in Config.Clients)
        {
            if (!await context.Clients.AnyAsync(c => c.ClientId == client.ClientId, ct))
                context.Clients.Add(client.ToEntity());
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("IdentityServer configuration store is seeded");
    }
}
