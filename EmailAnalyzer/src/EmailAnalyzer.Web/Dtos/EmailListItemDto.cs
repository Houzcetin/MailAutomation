using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Web.Common;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>Lightweight row for the email list (no Body / raw response).</summary>
public class EmailListItemDto
{
    public int Id { get; set; }

    public string SenderEmail { get; set; } = string.Empty;

    public string? SenderName { get; set; }

    public string Subject { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; }

    public MainCategory MainCategory { get; set; }

    public string MainCategoryDisplay { get; set; } = string.Empty;

    public string SubCategory { get; set; } = string.Empty;

    public Priority Priority { get; set; }

    public string PriorityDisplay { get; set; } = string.Empty;

    public Sentiment Sentiment { get; set; }

    public string SentimentDisplay { get; set; } = string.Empty;

    public bool RequiresHumanReview { get; set; }

    public bool IsProcessed { get; set; }

    public static EmailListItemDto FromEntity(EmailMessage e) => new()
    {
        Id = e.Id,
        SenderEmail = e.SenderEmail,
        SenderName = e.SenderName,
        Subject = e.Subject,
        ReceivedDate = e.ReceivedDate,
        MainCategory = e.MainCategory,
        MainCategoryDisplay = DisplayNames.For(e.MainCategory),
        SubCategory = e.SubCategory,
        Priority = e.Priority,
        PriorityDisplay = DisplayNames.For(e.Priority),
        Sentiment = e.Sentiment,
        SentimentDisplay = DisplayNames.For(e.Sentiment),
        RequiresHumanReview = e.RequiresHumanReview,
        IsProcessed = e.IsProcessed
    };
}
