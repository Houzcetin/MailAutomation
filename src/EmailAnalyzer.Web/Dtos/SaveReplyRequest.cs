using System.ComponentModel.DataAnnotations;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>Body of PUT /api/emails/{id}/reply and POST /api/emails/{id}/reply/send.</summary>
public class SaveReplyRequest
{
    [Required]
    public string ReplyBody { get; set; } = string.Empty;

    /// <summary>True while the text is the unedited AI draft; false once hand-edited.</summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>Raw model response of the generation this draft came from (if any).</summary>
    public string? AiRawResponse { get; set; }
}
