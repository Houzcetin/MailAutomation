namespace EmailAnalyzer.Domain.Entities;

/// <summary>
/// Tracks how far the polling worker has consumed a given IMAP folder, so only mail newer
/// than the last processed message is fetched — the historical backlog of old unread mail is
/// never reprocessed and nothing that arrives while the app is down is missed.
/// </summary>
public class MailboxState
{
    public int Id { get; set; }

    /// <summary>IMAP folder this watermark applies to (e.g. "INBOX").</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// IMAP UIDVALIDITY of the folder when <see cref="LastProcessedUid"/> was recorded.
    /// If the server reports a different value, all UIDs are invalidated and the watermark
    /// must be reset (treated as a first run).
    /// </summary>
    public uint UidValidity { get; set; }

    /// <summary>Highest IMAP UID that has been fetched/processed for this folder.</summary>
    public uint LastProcessedUid { get; set; }
}
