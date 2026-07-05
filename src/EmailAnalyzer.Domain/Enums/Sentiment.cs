namespace EmailAnalyzer.Domain.Enums;

/// <summary>Detected sentiment / tone of an incoming email. Persisted as string.</summary>
public enum Sentiment
{
    Positive,
    Neutral,
    Negative,
    Angry,
    Urgent
}
