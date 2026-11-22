using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Auth.Api.Controllers;

/// <summary>
/// Auditor registration, login, refresh rotation, email confirmation, and password recovery.
/// Access tokens are returned in the body. Refresh tokens are written only as an HttpOnly cookie.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    IJwtTokenService jwt,
    IRefreshTokenService refreshTokens,
    IEmailSender email,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string RefreshCookieName = "refreshToken";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.Username.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName?.Trim()
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
                return Conflict(new { title = "An auditor with that username or email already exists." });
            return BadRequest(new { errors = created.Errors.Select(e => e.Description) });
        }

        var roleResult = await users.AddToRoleAsync(user, IdentitySeeder.UserRole);
        if (!roleResult.Succeeded)
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description) });

        await SendConfirmationAsync(user, ct);

        return Created($"/users/{user.Id}", new
        {
            user.Id,
            user.UserName,
            user.Email,
            emailConfirmationRequired = true
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.Username.Trim());
        if (user is null)
            return Unauthorized(new { title = "Invalid username or password." });

        if (!user.EmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                title = "Email is not confirmed.",
                detail = "Confirm the address before signing in to the procurement platform."
            });

        if (await users.IsLockedOutAsync(user))
            return StatusCode(StatusCodes.Status423Locked, new { title = "Account locked after repeated failed sign-ins." });

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            if (await users.IsLockedOutAsync(user))
                return StatusCode(StatusCodes.Status423Locked, new { title = "Account locked after repeated failed sign-ins." });
            return Unauthorized(new { title = "Invalid username or password." });
        }

        await users.ResetAccessFailedCountAsync(user);
        return await IssueAsync(user, ct);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return Unauthorized(new { title = "Refresh token cookie is missing." });

        var rotated = await refreshTokens.RotateAsync(raw, ClientIp(), ct);
        if (rotated is null)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { title = "Refresh token is invalid, expired, or already used." });
        }

        var user = await users.FindByIdAsync(rotated.UserId);
        if (user is null || !user.EmailConfirmed)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { title = "Auditor account is not available." });
        }

        WriteRefreshCookie(rotated.Token);
        var roles = await users.GetRolesAsync(user);
        return Ok(TokenBody(jwt.GenerateAccessToken(user, roles)));
    }

    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var raw) && !string.IsNullOrWhiteSpace(raw))
            await refreshTokens.RevokeAsync(raw, ClientIp(), ct);

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var raw) && !string.IsNullOrWhiteSpace(raw))
            await refreshTokens.RevokeAsync(raw, ClientIp(), ct);

        DeleteRefreshCookie();
        logger.LogInformation("Auditor {UserId} logged out", CurrentUserId());
        return NoContent();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest(new { title = "Confirmation token is invalid." });

        var confirmed = await users.ConfirmEmailAsync(user, request.Token);
        if (!confirmed.Succeeded)
            return BadRequest(new { title = "Confirmation token is invalid or expired.", errors = confirmed.Errors.Select(e => e.Description) });

        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
            await SendConfirmationAsync(user, ct);

        return Accepted(new { title = "If the account exists and is unconfirmed, a new message was sent." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var body =
                $"Password reset for auditor {user.UserName}.{Environment.NewLine}" +
                $"This token is single-use and expires with the Identity token lifespan.{Environment.NewLine}" +
                $"POST /auth/reset-password{Environment.NewLine}" +
                $"email: {user.Email}{Environment.NewLine}" +
                $"token: {token}";
            await email.SendAsync(user.Email!, "Reset your procurement-audit password", body, ct);
        }

        return Accepted(new { title = "If the account exists, a reset message was sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return BadRequest(new { title = "Reset token is invalid." });

        var reset = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!reset.Succeeded)
            return BadRequest(new { title = "Reset token is invalid or expired.", errors = reset.Errors.Select(e => e.Description) });

        await refreshTokens.RevokeAllAsync(user.Id, ClientIp(), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(CurrentUserId() ?? "");
        if (user is null)
            return Unauthorized();

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
            return BadRequest(new { errors = changed.Errors.Select(e => e.Description) });

        return NoContent();
    }

    /// <summary>Development-only view of the mock mailbox (confirmation and reset tokens).</summary>
    [HttpGet("dev/mailbox")]
    [AllowAnonymous]
    public IActionResult Mailbox()
    {
        if (!environment.IsDevelopment())
            return NotFound();

        return Ok(email.Messages);
    }

    private async Task<IActionResult> IssueAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user);
        var refresh = await refreshTokens.IssueAsync(user, ClientIp(), ct);
        WriteRefreshCookie(refresh.Token);
        return Ok(TokenBody(jwt.GenerateAccessToken(user, roles)));
    }

    private static AccessTokenResponse TokenBody(string accessToken)
        => new(accessToken, ExpiresIn: 15 * 60, TokenType: "Bearer");

    private async Task SendConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = QueryHelpers.AddQueryString("/auth/verify-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id,
            ["token"] = token
        });

        var body =
            $"Confirm {user.Email} before signing in to the reconstruction-procurement audit platform.{Environment.NewLine}" +
            $"The token is single-use.{Environment.NewLine}" +
            $"POST /auth/verify-email{Environment.NewLine}" +
            $"userId: {user.Id}{Environment.NewLine}" +
            $"token: {token}{Environment.NewLine}" +
            $"link: {link}";

        await email.SendAsync(user.Email!, "Confirm your auditor email", body, ct);
    }

    private void WriteRefreshCookie(string token)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/auth",
            IsEssential = true
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/auth"
        });
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? CurrentUserId()
        => User.FindFirst("userId")?.Value
           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? User.FindFirst("sub")?.Value;
}
