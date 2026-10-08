using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FileFlowHR.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, DateTime SentAtUtc);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Local development: emails are not sent, they are kept in memory and shown at /dev/mailbox,
/// so the team can click invitation and reset links. Swap for Azure Communication Services later.
/// </summary>
public sealed class DevMailbox(ILogger<DevMailbox> logger) : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => _messages.Reverse().ToList();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _messages.Enqueue(new EmailMessage(to, subject, htmlBody, DateTime.UtcNow));
        while (_messages.Count > 100) _messages.TryDequeue(out _);
        logger.LogInformation("Email to {To}: {Subject} (see /dev/mailbox)", to, subject);
        return Task.CompletedTask;
    }
}
