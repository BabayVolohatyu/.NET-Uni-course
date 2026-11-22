using Auth.Api.Data;

namespace Auth.Api.Services;

public interface IRefreshTokenService
{
    Task<RefreshToken> IssueAsync(ApplicationUser user, string? ipAddress, CancellationToken ct);

    /// <summary>
    /// Revokes the presented token and stores a new one. Returns null when the token is missing,
    /// expired, or already revoked. Reuse of a revoked token revokes every active token of that user.
    /// </summary>
    Task<RefreshToken?> RotateAsync(string rawToken, string? ipAddress, CancellationToken ct);

    Task<bool> RevokeAsync(string rawToken, string? ipAddress, CancellationToken ct);

    Task RevokeAllAsync(string userId, string? ipAddress, CancellationToken ct);
}
