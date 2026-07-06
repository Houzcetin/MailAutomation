using EmailAnalyzer.Domain.Dtos;

namespace EmailAnalyzer.Domain.Services;

/// <summary>Fetches emails from the mailbox using a UID watermark.</summary>
public interface IEmailFetchService
{
    /// <summary>
    /// Reads messages from the configured folder as plain-text DTOs.
    /// <para>
    /// When <paramref name="afterUid"/> is supplied, only messages whose UID is greater than it
    /// are returned (nothing is missed, even mail that arrived while the app was down). When it
    /// is <c>null</c> (first run for this folder), the newest <c>MaxEmailsPerCycle</c> messages
    /// are returned so the large historical backlog is skipped.
    /// </para>
    /// The returned <see cref="FetchResult.UidValidity"/> lets the caller detect a UID-space
    /// reset (changed UIDVALIDITY) and re-baseline its watermark.
    /// </summary>
    Task<FetchResult> FetchAsync(uint? afterUid, CancellationToken cancellationToken = default);
}
