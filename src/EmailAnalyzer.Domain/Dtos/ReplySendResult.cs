namespace EmailAnalyzer.Domain.Dtos;

/// <summary>Outcome of an SMTP reply send attempt.</summary>
public class ReplySendResult
{
    public bool Success { get; set; }

    /// <summary>Message-Id header of the outgoing mail when the send succeeded.</summary>
    public string? SentMessageId { get; set; }

    public DateTime SentDate { get; set; }

    /// <summary>Human-readable failure description when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }
}
