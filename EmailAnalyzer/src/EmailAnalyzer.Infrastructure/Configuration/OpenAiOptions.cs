namespace EmailAnalyzer.Infrastructure.Configuration;

/// <summary>Binds the "OpenAi" configuration section.</summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 1024;

    /// <summary>Base URL of the Chat Completions API.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
}
