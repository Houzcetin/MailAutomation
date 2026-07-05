using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Domain.Dtos;

/// <summary>
/// Outcome of analysing a single email. Mirrors the JSON contract the model must
/// return (spec section 6), plus application-side flags.
/// </summary>
public class EmailAnalysisResult
{
    public MainCategory MainCategory { get; set; } = MainCategory.Diger;

    public string SubCategory { get; set; } = string.Empty;

    public Priority Priority { get; set; } = Priority.Low;

    public Sentiment Sentiment { get; set; } = Sentiment.Neutral;

    public string Summary { get; set; } = string.Empty;

    /// <summary>Model confidence, 0-1.</summary>
    public decimal Confidence { get; set; }

    public bool RequiresHumanReview { get; set; }

    /// <summary>True when the AI call and JSON parsing both succeeded.</summary>
    public bool IsProcessed { get; set; }

    /// <summary>Raw model response text, kept for debugging.</summary>
    public string? RawResponse { get; set; }
}
