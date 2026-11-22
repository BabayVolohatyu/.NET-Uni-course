using Auth.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

public sealed class RefreshTokenService(ApplicationDbContext db, IJwtTokenService jwt) : IRefreshTokenService
{
    public async Task<RefreshToken> IssueAsync(ApplicationUser user, string? ipAddress, CancellationToken ct)
    {
        var token = jwt.GenerateRefreshToken(user.Id, ipAddress);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<RefreshToken?> RotateAsync(string rawToken, string? ipAddress, CancellationToken ct)
    {
        var existing = await db.RefreshTokens
            .SingleOrDefaultAsync(t => t.Token == rawToken, ct);

        if (existing is null)
            return null;

        if (existing.RevokedAt is not null)
        {
            // A rotated token was presented again — treat it as theft and drop the family.
            await RevokeAllAsync(existing.UserId, ipAddress, ct);
            return null;
        }

        if (existing.IsExpired)
            return null;

        var replacement = jwt.GenerateRefreshToken(existing.UserId, ipAddress);
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByToken = replacement.Token;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);
        return replacement;
    }

    public async Task<bool> RevokeAsync(string rawToken, string? ipAddress, CancellationToken ct)
    {
        var existing = await db.RefreshTokens
            .SingleOrDefaultAsync(t => t.Token == rawToken, ct);

        if (existing is null || existing.RevokedAt is not null)
            return false;

        existing.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RevokeAllAsync(string userId, string? ipAddress, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.RevokedAt = now;

        await db.SaveChangesAsync(ct);
    }
}
