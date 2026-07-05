namespace EmailAnalyzer.Infrastructure.Configuration;

/// <summary>Binds the "Gmail" configuration section (IMAP connection + polling).</summary>
public class GmailOptions
{
    public const string SectionName = "Gmail";

    public string Host { get; set; } = "imap.gmail.com";

    public int Port { get; set; } = 993;

    /// <summary>Mailbox address used for IMAP login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gmail App Password (requires 2FA). Kept in user-secrets, never in the repo.</summary>
    public string AppPassword { get; set; } = string.Empty;

    /// <summary>How often the polling worker checks for new mail.</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>IMAP folder to read from.</summary>
    public string Folder { get; set; } = "INBOX";

    /// <summary>
    /// Max messages to fetch/analyse per polling cycle. Guards against processing a huge
    /// unseen backlog (and its AI cost) in one go — the newest N are taken each cycle.
    /// </summary>
    public int MaxEmailsPerCycle { get; set; } = 20;

    /// <summary>SMTP host for sending replies; same credentials as IMAP.</summary>
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    /// <summary>587 = STARTTLS (465/SslOnConnect also works with Gmail).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Display name shown in the From header of outgoing replies.</summary>
    public string SenderDisplayName { get; set; } = string.Empty;
}
