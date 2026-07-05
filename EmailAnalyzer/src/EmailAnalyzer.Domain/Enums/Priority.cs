namespace EmailAnalyzer.Domain.Enums;

/// <summary>Business priority of an incoming email. Persisted as string.</summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}
