namespace EmailAnalyzer.Infrastructure.Configuration;

/// <summary>Binds the "Analysis" configuration section.</summary>
public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    /// <summary>Below this confidence the record is flagged for human review.</summary>
    public decimal ConfidenceThreshold { get; set; } = 0.70m;

    /// <summary>Body text is truncated to this many characters before analysis.</summary>
    public int MaxBodyChars { get; set; } = 8000;
}
