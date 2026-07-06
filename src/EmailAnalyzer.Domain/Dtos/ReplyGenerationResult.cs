namespace EmailAnalyzer.Domain.Dtos;

/// <summary>Outcome of an AI reply-draft generation.</summary>
public class ReplyGenerationResult
{
    public bool Success { get; set; }

    /// <summary>The drafted reply text (plain text); empty when generation failed.</summary>
    public string ReplyBody { get; set; } = string.Empty;

    /// <summary>Raw model response, kept for debugging/audit.</summary>
    public string? RawResponse { get; set; }

    /// <summary>Internal error description when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }
}
