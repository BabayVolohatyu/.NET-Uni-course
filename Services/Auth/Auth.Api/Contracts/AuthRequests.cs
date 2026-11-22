using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, MinLength(3)]
    public string Username { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(8)]
    public string Password { get; set; } = "";

    public string? DisplayName { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public sealed class VerifyEmailRequest
{
    [Required]
    public string UserId { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";
}

public sealed class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = "";
}

public sealed record AccessTokenResponse(string AccessToken, int ExpiresIn, string TokenType);
