using EmailAnalyzer.Domain.Dtos;

namespace EmailAnalyzer.Domain.Services;

/// <summary>Fetches unseen emails from the mailbox and marks them as read.</summary>
public interface IEmailFetchService
{
    /// <summary>Reads all UNSEEN messages from the configured folder as plain-text DTOs.</summary>
    Task<IReadOnlyList<FetchedEmail>> FetchUnseenAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks a message as seen (\Seen flag) so it is not fetched again.</summary>
    Task MarkAsSeenAsync(string messageId, CancellationToken cancellationToken = default);
}
