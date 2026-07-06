namespace EmailAnalyzer.Domain.Dtos;

/// <summary>
/// Outcome of one fetch cycle: the folder's current UIDVALIDITY plus the messages read.
/// The caller compares <see cref="UidValidity"/> against its stored watermark to decide
/// whether the UID space is still valid.
/// </summary>
public class FetchResult
{
    /// <summary>Current IMAP UIDVALIDITY of the folder.</summary>
    public uint UidValidity { get; set; }

    /// <summary>Highest UID currently present in the folder, or 0 if the folder is empty.</summary>
    public uint HighestUid { get; set; }

    /// <summary>Messages read this cycle (already filtered/capped by the fetch service).</summary>
    public IReadOnlyList<FetchedEmail> Emails { get; set; } = [];
}
