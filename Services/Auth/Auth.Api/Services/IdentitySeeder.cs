using Auth.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

/// <summary>
/// Creates the User and Admin roles on every startup. Development accounts are confirmed
/// so the login control point can be exercised without a mailbox.
/// </summary>
public static class IdentitySeeder
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { UserRole, AdminRole })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var created = await roles.CreateAsync(new IdentityRole(role));
                if (!created.Succeeded)
                    throw new InvalidOperationException($"Could not create role {role}: {Format(created)}");
                logger.LogInformation("Created Identity role {Role}", role);
            }
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(users, logger, new SeedUser(
            "auditor.admin",
            "auditor.admin@procurement.audit",
            "Reconstruction Audit Admin",
            "Admin123!",
            [AdminRole, UserRole]), ct);

        await EnsureUserAsync(users, logger, new SeedUser(
            "auditor.user",
            "auditor.user@procurement.audit",
            "Kyiv City Council clerk",
            "User12345!",
            [UserRole]), ct);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> users,
        ILogger logger,
        SeedUser seed,
        CancellationToken ct)
    {
        var existing = await users.Users.FirstOrDefaultAsync(u => u.UserName == seed.UserName, ct);
        if (existing is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = seed.UserName,
            Email = seed.Email,
            DisplayName = seed.DisplayName,
            EmailConfirmed = true
        };

        var created = await users.CreateAsync(user, seed.Password);
        if (!created.Succeeded)
            throw new InvalidOperationException($"Could not seed {seed.UserName}: {Format(created)}");

        var assigned = await users.AddToRolesAsync(user, seed.Roles);
        if (!assigned.Succeeded)
            throw new InvalidOperationException($"Could not assign roles to {seed.UserName}: {Format(assigned)}");

        logger.LogInformation("Seeded auditor {UserName} with roles {Roles}", seed.UserName, string.Join(',', seed.Roles));
    }

    private static string Format(IdentityResult result)
        => string.Join("; ", result.Errors.Select(e => e.Description));

    private sealed record SeedUser(
        string UserName,
        string Email,
        string DisplayName,
        string Password,
        string[] Roles);
}
