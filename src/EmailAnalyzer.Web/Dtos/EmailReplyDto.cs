using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Web.Common;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>The reply draft/sent state of an email as shown in the admin panel.</summary>
public class EmailReplyDto
{
    public string Body { get; set; } = string.Empty;

    public ReplyStatus Status { get; set; }

    public string StatusDisplay { get; set; } = string.Empty;

    public bool IsAiGenerated { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public DateTime? SentDate { get; set; }

    public string? LastSendError { get; set; }

    public static EmailReplyDto FromEntity(EmailReply r) => new()
    {
        Body = r.Body,
        Status = r.Status,
        StatusDisplay = DisplayNames.For(r.Status),
        IsAiGenerated = r.IsAiGenerated,
        UpdatedDate = r.UpdatedDate,
        SentDate = r.SentDate,
        LastSendError = r.LastSendError
    };
}
