using Microsoft.AspNetCore.Identity;

namespace Platform.IdentityServer;

/// <summary>
/// Auditor account stored in IdentityServerDb, separate from the Auth Service's IdentityDb.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
