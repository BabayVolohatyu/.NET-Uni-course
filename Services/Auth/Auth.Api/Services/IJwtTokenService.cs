using System.Security.Claims;
using Auth.Api.Data;

namespace Auth.Api.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);

    RefreshToken GenerateRefreshToken(string userId, string? ipAddress);

    ClaimsPrincipal? ValidateToken(string token);
}
