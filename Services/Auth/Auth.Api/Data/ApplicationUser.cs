using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Data;

/// <summary>
/// Auditor account. Extends <see cref="IdentityUser"/> with the name shown on procurement dossiers.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
