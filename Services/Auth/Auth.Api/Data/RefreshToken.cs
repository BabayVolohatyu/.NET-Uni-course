namespace Auth.Api.Data;

/// <summary>
/// Opaque refresh token stored in IdentityDb. The raw value travels only in an HttpOnly cookie.
/// Rotation sets <see cref="RevokedAt"/> on the old row and <see cref="ReplacedByToken"/> to the new value.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Seven days after <see cref="CreatedAt"/>.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt != null;

    public bool IsActive => !IsRevoked && !IsExpired;
}
