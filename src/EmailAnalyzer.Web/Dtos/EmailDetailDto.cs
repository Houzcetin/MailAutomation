using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Web.Common;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>Full detail of a single email, including body and AI metadata.</summary>
public class EmailDetailDto
{
    public int Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string? SenderName { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; }

    public MainCategory MainCategory { get; set; }

    public string MainCategoryDisplay { get; set; } = string.Empty;

    public string SubCategory { get; set; } = string.Empty;

    public Priority Priority { get; set; }

    public string PriorityDisplay { get; set; } = string.Empty;

    public Sentiment Sentiment { get; set; }

    public string SentimentDisplay { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public decimal AiConfidenceScore { get; set; }

    public bool IsProcessed { get; set; }

    public DateTime? ProcessedDate { get; set; }

    public bool RequiresHumanReview { get; set; }

    public string? AiRawResponse { get; set; }

    public DateTime CreatedDate { get; set; }

    public static EmailDetailDto FromEntity(EmailMessage e) => new()
    {
        Id = e.Id,
        MessageId = e.MessageId,
        SenderEmail = e.SenderEmail,
        SenderName = e.SenderName,
        Subject = e.Subject,
        Body = e.Body,
        ReceivedDate = e.ReceivedDate,
        MainCategory = e.MainCategory,
        MainCategoryDisplay = DisplayNames.For(e.MainCategory),
        SubCategory = e.SubCategory,
        Priority = e.Priority,
        PriorityDisplay = DisplayNames.For(e.Priority),
        Sentiment = e.Sentiment,
        SentimentDisplay = DisplayNames.For(e.Sentiment),
        Summary = e.Summary,
        AiConfidenceScore = e.AiConfidenceScore,
        IsProcessed = e.IsProcessed,
        ProcessedDate = e.ProcessedDate,
        RequiresHumanReview = e.RequiresHumanReview,
        AiRawResponse = e.AiRawResponse,
        CreatedDate = e.CreatedDate
    };
}
