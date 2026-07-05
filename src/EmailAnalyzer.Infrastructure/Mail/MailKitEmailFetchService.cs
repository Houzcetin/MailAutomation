using System.Text.RegularExpressions;
using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Configuration;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Infrastructure.Mail;

/// <summary>
/// MailKit/IMAP implementation of <see cref="IEmailFetchService"/>. Connects, works, and
/// disconnects on every call so a dropped connection never leaves the worker in a bad state
/// (spec section 7). Bodies are converted to plain text; attachments are counted, not downloaded.
/// </summary>
public class MailKitEmailFetchService : IEmailFetchService
{
    private readonly GmailOptions _options;
    private readonly ILogger<MailKitEmailFetchService> _logger;

    public MailKitEmailFetchService(
        IOptions<GmailOptions> options,
        ILogger<MailKitEmailFetchService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FetchedEmail>> FetchUnseenAsync(
        CancellationToken cancellationToken = default)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, cancellationToken);

        var folder = await OpenFolderAsync(client, FolderAccess.ReadWrite, cancellationToken);
        _logger.LogDebug("Folder '{Folder}' opened, searching unseen...", _options.Folder);

        var uids = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken);
        _logger.LogDebug("Found {Count} unseen UID(s).", uids.Count);

        // Only take the newest N to avoid processing a large backlog (and its AI cost) at once.
        // Unseen UIDs come back ascending, so the newest ones are at the end of the list.
        var batch = uids
            .Skip(Math.Max(0, uids.Count - _options.MaxEmailsPerCycle))
            .ToList();

        if (batch.Count < uids.Count)
        {
            _logger.LogInformation(
                "Processing newest {Batch} of {Total} unseen (MaxEmailsPerCycle).",
                batch.Count, uids.Count);
        }

        var results = new List<FetchedEmail>(batch.Count);

        foreach (var uid in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var message = await folder.GetMessageAsync(uid, cancellationToken);

            results.Add(new FetchedEmail
            {
                MessageId = message.MessageId ?? uid.ToString(),
                SenderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
                SenderName = message.From.Mailboxes.FirstOrDefault()?.Name,
                Subject = message.Subject ?? string.Empty,
                Body = ExtractPlainText(message.TextBody, message.HtmlBody),
                ReceivedDate = message.Date.UtcDateTime,
                AttachmentCount = message.Attachments.Count()
            });
        }

        await client.DisconnectAsync(true, cancellationToken);
        return results;
    }

    public async Task MarkAsSeenAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, cancellationToken);

        var folder = await OpenFolderAsync(client, FolderAccess.ReadWrite, cancellationToken);

        // Find the message by its Message-Id header and set the \Seen flag.
        var uids = await folder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), cancellationToken);
        if (uids.Count > 0)
        {
            await folder.AddFlagsAsync(uids, MessageFlags.Seen, true, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Could not find message {MessageId} to mark as seen.", messageId);
        }

        await client.DisconnectAsync(true, cancellationToken);
    }

    private async Task ConnectAsync(ImapClient client, CancellationToken cancellationToken)
    {
        // Fail fast instead of hanging forever if the server never responds.
        client.Timeout = 30_000;

        _logger.LogDebug("Connecting to IMAP {Host}:{Port}...", _options.Host, _options.Port);
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.SslOnConnect, cancellationToken);

        _logger.LogDebug("Authenticating as {Email}...", _options.Email);
        await client.AuthenticateAsync(_options.Email, _options.AppPassword, cancellationToken);

        _logger.LogDebug("IMAP authenticated.");
    }

    private async Task<IMailFolder> OpenFolderAsync(
        ImapClient client, FolderAccess access, CancellationToken cancellationToken)
    {
        var folder = _options.Folder.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(_options.Folder, cancellationToken);

        await folder.OpenAsync(access, cancellationToken);
        return folder;
    }

    /// <summary>Prefers the plain-text body; falls back to HTML with tags stripped.</summary>
    private static string ExtractPlainText(string? textBody, string? htmlBody)
    {
        if (!string.IsNullOrWhiteSpace(textBody))
        {
            return textBody;
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            return string.Empty;
        }

        // Minimal HTML strip: drop tags and collapse whitespace. Good enough for analysis;
        // the AI service truncates to MaxBodyChars afterwards.
        var withoutTags = Regex.Replace(htmlBody, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
