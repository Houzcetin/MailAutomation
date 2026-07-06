namespace EmailAnalyzer.Web.Dtos;

/// <summary>Result of POST /api/emails/{id}/reply/generate.</summary>
public class GenerateReplyResponse
{
    /// <summary>The AI-drafted reply text, ready for the editor.</summary>
    public string ReplyBody { get; set; } = string.Empty;

    /// <summary>
    /// Raw model response; the client passes it back on save so it can be persisted
    /// alongside the draft for debugging.
    /// </summary>
    public string? RawResponse { get; set; }
}
