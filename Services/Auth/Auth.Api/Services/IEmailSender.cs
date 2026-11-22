namespace Auth.Api.Services;

public sealed record SentEmail(DateTimeOffset SentAt, string To, string Subject, string Body);

/// <summary>
/// Development stand-in for SMTP. Confirmation and reset links are logged and kept in memory
/// so the one-time token can be copied into <c>/auth/verify-email</c> or <c>/auth/reset-password</c>.
/// </summary>
public interface IEmailSender
{
    IReadOnlyList<SentEmail> Messages { get; }

    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
