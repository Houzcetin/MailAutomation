using System.ComponentModel.DataAnnotations;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>Body of POST /api/emails/analyze — manual, end-to-end analysis test.</summary>
public class AnalyzeRequest
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string? SenderEmail { get; set; }

    public string? SenderName { get; set; }
}
