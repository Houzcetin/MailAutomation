using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailAnalyzer.Infrastructure.Mail;

/// <summary>
/// MailKit/SMTP implementation of <see cref="IEmailReplySender"/>. Connects, sends, and
/// disconnects on every call (same resilience approach as the IMAP fetch service). Sets
/// In-Reply-To/References so the reply lands in the original Gmail conversation thread.
/// Never throws — failures come back as a result the caller persists.
/// </summary>
public class MailKitEmailReplySender : IEmailReplySender
{
    /// <summary>Message-Id prefix of records created by the manual /analyze endpoint.</summary>
    private const string SyntheticMessageIdPrefix = "manual-";

    private readonly GmailOptions _options;
    private readonly ILogger<MailKitEmailReplySender> _logger;

    public MailKitEmailReplySender(
        IOptions<GmailOptions> options,
        ILogger<MailKitEmailReplySender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReplySendResult> SendReplyAsync(
        EmailMessage original,
        string replyBody,
        CancellationToken cancellationToken = default)
    {
        if (!MailboxAddress.TryParse(original.SenderEmail, out var recipient) ||
            !IsRoutableAddress(recipient.Address))
        {
            return Failure($"Geçersiz alıcı adresi: {original.SenderEmail}");
        }

        recipient.Name = original.SenderName;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.SenderDisplayName, _options.Email));
        message.To.Add(recipient);
        message.Subject = BuildReplySubject(original.Subject);
        message.Body = new TextPart("plain") { Text = replyBody };

        // Thread the reply into the original conversation — but not for synthetic ids
        // created by the manual /analyze endpoint, which never existed as real mails.
        if (!string.IsNullOrWhiteSpace(original.MessageId) &&
            !original.MessageId.StartsWith(SyntheticMessageIdPrefix, StringComparison.Ordinal))
        {
            message.InReplyTo = original.MessageId;
            message.References.Add(original.MessageId);
        }

        try
        {
            using var client = new SmtpClient();
            client.Timeout = 30_000;

            _logger.LogDebug("Connecting to SMTP {Host}:{Port}...", _options.SmtpHost, _options.SmtpPort);
            await client.ConnectAsync(
                _options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_options.Email, _options.AppPassword, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Reply for email {EmailId} sent to {Recipient}.", original.Id, original.SenderEmail);

            return new ReplySendResult
            {
                Success = true,
                SentMessageId = message.MessageId,
                SentDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send reply for email {EmailId} to {Recipient}.",
                original.Id, original.SenderEmail);

            return Failure(ex.Message);
        }
    }

    /// <summary>Prepends "Re: " unless already present. Ordinal comparison on purpose —
    /// Turkish culture would not match "RE:"/"re:" case-insensitively (İ/i pitfall).</summary>
    public static string BuildReplySubject(string subject)
    {
        var trimmed = subject?.Trim() ?? string.Empty;
        return trimmed.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"Re: {trimmed}";
    }

    /// <summary>
    /// MimeKit's TryParse accepts dotless domains like "manual-test@local" (synthetic test
    /// records), which Gmail would accept and then bounce. Require a dotted domain.
    /// </summary>
    public static bool IsRoutableAddress(string address)
    {
        var at = address.LastIndexOf('@');
        return at > 0 && at < address.Length - 1 && address.IndexOf('.', at + 1) > at + 1;
    }

    private static ReplySendResult Failure(string error) => new()
    {
        Success = false,
        ErrorMessage = error,
        SentDate = DateTime.UtcNow
    };
}
