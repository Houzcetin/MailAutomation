using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Entities;

namespace EmailAnalyzer.Domain.Services;

/// <summary>
/// Sends a reply to the sender of the original email. Returns a result instead of throwing:
/// SMTP failures are expected operational outcomes the caller must persist and surface.
/// </summary>
public interface IEmailReplySender
{
    Task<ReplySendResult> SendReplyAsync(
        EmailMessage original,
        string replyBody,
        CancellationToken cancellationToken = default);
}
