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

    public async Task<FetchResult> FetchAsync(
        uint? afterUid, CancellationToken cancellationToken = default)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, cancellationToken);

        // Read-only: we never set the \Seen flag, so mail stays unread in the user's mailbox.
        var folder = await OpenFolderAsync(client, FolderAccess.ReadOnly, cancellationToken);
        var uidValidity = folder.UidValidity;
        _logger.LogDebug(
            "Folder '{Folder}' opened. UidValidity={UidValidity}, afterUid={AfterUid}.",
            _options.Folder, uidValidity, afterUid);

        // Select which UIDs to fetch:
        //  - First run (afterUid == null): take the newest N so the historical backlog is skipped.
        //  - Otherwise: everything strictly greater than the watermark — nothing is missed, even
        //    mail that arrived while the app was down.
        IList<UniqueId> uids;
        if (afterUid is { } watermark)
        {
            var range = new UniqueIdRange(new UniqueId(watermark + 1), UniqueId.MaxValue);
            var found = await folder.SearchAsync(range, SearchQuery.All, cancellationToken);
            // IMAP resolves the "*" upper bound to the highest UID in the folder and always
            // includes it, even when it is below the range's lower bound — so a "watermark+1:*"
            // search returns the newest message even if nothing is actually newer. Filter it out
            // to enforce strictly-greater-than semantics.
            uids = found.Where(u => u.Id > watermark).ToList();
            _logger.LogDebug("Found {Count} UID(s) newer than {Watermark}.", uids.Count, watermark);
        }
        else
        {
            var all = await folder.SearchAsync(SearchQuery.All, cancellationToken);
            uids = all
                .Skip(Math.Max(0, all.Count - _options.MaxEmailsPerCycle))
                .ToList();
            _logger.LogInformation(
                "First run for '{Folder}': baselining on newest {Batch} of {Total} message(s).",
                _options.Folder, uids.Count, all.Count);
        }

        // Cap per cycle so a large gap (e.g. app down for a long time) doesn't fetch everything
        // and its AI cost at once — the remainder is picked up next cycle as the watermark advances.
        var highestUidInFolder = uids.Count > 0 ? uids.Max(u => u.Id) : (afterUid ?? 0);
        var batch = uids
            .OrderBy(u => u.Id)
            .Take(_options.MaxEmailsPerCycle)
            .ToList();

        var results = new List<FetchedEmail>(batch.Count);

        foreach (var uid in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var message = await folder.GetMessageAsync(uid, cancellationToken);

            results.Add(new FetchedEmail
            {
                Uid = uid.Id,
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

        return new FetchResult
        {
            UidValidity = uidValidity,
            HighestUid = highestUidInFolder,
            Emails = results
        };
    }

    public async Task MarkAsSeenAsync(uint uid, CancellationToken cancellationToken = default)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, cancellationToken);

        // ReadWrite so we can set the \Seen flag.
        var folder = await OpenFolderAsync(client, FolderAccess.ReadWrite, cancellationToken);
        await folder.AddFlagsAsync(new UniqueId(uid), MessageFlags.Seen, silent: true, cancellationToken);

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
