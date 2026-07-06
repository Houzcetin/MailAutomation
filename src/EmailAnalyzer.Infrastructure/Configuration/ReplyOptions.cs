namespace EmailAnalyzer.Infrastructure.Configuration;

/// <summary>Binds the "Reply" configuration section (AI reply drafting).</summary>
public class ReplyOptions
{
    public const string SectionName = "Reply";

    /// <summary>
    /// Sampling temperature for reply prose. Classification runs at 0; replies need
    /// some variation to avoid robotic, repetitive wording.
    /// </summary>
    public double Temperature { get; set; } = 0.5;

    /// <summary>Replies are longer than classification JSON, so a higher cap than OpenAi:MaxTokens.</summary>
    public int MaxTokens { get; set; } = 1200;

    /// <summary>Company name used in the sign-off; empty → the model uses a generic closing.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Person name used in the sign-off; empty → the model leaves an [Ad Soyad] placeholder.</summary>
    public string SignOffName { get; set; } = string.Empty;
}
