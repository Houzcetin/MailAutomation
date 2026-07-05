using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Domain.Entities;

/// <summary>
/// A single incoming email together with its AI analysis result.
/// Maps to table EmailMessages (see spec section 4).
/// </summary>
public class EmailMessage
{
    public int Id { get; set; }

    /// <summary>IMAP Message-Id header. Unique — the same mail is never processed twice.</summary>
    public string MessageId { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string? SenderName { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>Body converted to plain text (HTML stripped, truncated to MaxBodyChars).</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; }

    public MainCategory MainCategory { get; set; }

    /// <summary>Specific AI-produced topic (3-6 words, Turkish).</summary>
    public string SubCategory { get; set; } = string.Empty;

    public Priority Priority { get; set; }

    public Sentiment Sentiment { get; set; }

    /// <summary>1-2 sentence Turkish summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Model confidence, 0-1.</summary>
    public decimal AiConfidenceScore { get; set; }

    /// <summary>True when the AI analysis succeeded.</summary>
    public bool IsProcessed { get; set; }

    public DateTime? ProcessedDate { get; set; }

    public bool RequiresHumanReview { get; set; }

    /// <summary>Raw JSON response from the model, kept for debugging.</summary>
    public string? AiRawResponse { get; set; }

    /// <summary>UTC timestamp of when the record was created.</summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Builds an entity from the raw mail data and its AI analysis result. Shared by the
    /// polling worker and the manual /analyze endpoint so the mapping lives in one place.
    /// </summary>
    public static EmailMessage FromAnalysis(
        string messageId,
        string senderEmail,
        string? senderName,
        string subject,
        string body,
        DateTime receivedDate,
        EmailAnalysisResult result)
    {
        var now = DateTime.UtcNow;

        return new EmailMessage
        {
            MessageId = messageId,
            SenderEmail = senderEmail,
            SenderName = senderName,
            Subject = subject,
            Body = body,
            ReceivedDate = receivedDate,
            MainCategory = result.MainCategory,
            SubCategory = result.SubCategory,
            Priority = result.Priority,
            Sentiment = result.Sentiment,
            Summary = result.Summary,
            AiConfidenceScore = result.Confidence,
            IsProcessed = result.IsProcessed,
            ProcessedDate = result.IsProcessed ? now : null,
            RequiresHumanReview = result.RequiresHumanReview,
            AiRawResponse = result.RawResponse,
            CreatedDate = now
        };
    }
}
