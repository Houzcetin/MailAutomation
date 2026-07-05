namespace EmailAnalyzer.Domain.Dtos;

/// <summary>A raw email pulled from the mailbox, before AI analysis.</summary>
public class FetchedEmail
{
    public string MessageId { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string? SenderName { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>Plain-text body (HTML stripped, truncated to MaxBodyChars).</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; }

    public int AttachmentCount { get; set; }
}
