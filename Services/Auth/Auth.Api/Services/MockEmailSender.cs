using System.Collections.Concurrent;

namespace Auth.Api.Services;

public sealed class MockEmailSender(ILogger<MockEmailSender> logger) : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _messages = new();

    public IReadOnlyList<SentEmail> Messages => _messages.ToArray();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var message = new SentEmail(DateTimeOffset.UtcNow, to, subject, body);
        _messages.Enqueue(message);
        while (_messages.Count > 50 && _messages.TryDequeue(out _))
        {
        }

        logger.LogInformation(
            "Mock email to {Recipient} subject {Subject}. Body: {Body}",
            to, subject, body);

        return Task.CompletedTask;
    }
}
