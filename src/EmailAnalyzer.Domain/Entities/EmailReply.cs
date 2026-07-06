using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Domain.Entities;

/// <summary>
/// The (single) reply to an incoming email: an AI-drafted, human-edited text that is first
/// saved as a draft and later sent via SMTP. One row per email (unique FK), updated in place;
/// once Sent the row is frozen. Maps to table EmailReplies.
/// </summary>
public class EmailReply
{
    public int Id { get; set; }

    public int EmailMessageId { get; set; }

    public EmailMessage EmailMessage { get; set; } = null!;

    /// <summary>Plain-text reply body as shown in the editor.</summary>
    public string Body { get; set; } = string.Empty;

    public ReplyStatus Status { get; set; }

    /// <summary>True while the body is the unedited AI draft; false once hand-edited.</summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>Raw model response of the last generation, kept for debugging.</summary>
    public string? AiRawResponse { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public DateTime? SentDate { get; set; }

    /// <summary>Message-Id header of the outgoing SMTP mail.</summary>
    public string? SentMessageId { get; set; }

    /// <summary>Last SMTP failure description; cleared on successful send.</summary>
    public string? LastSendError { get; set; }
}
