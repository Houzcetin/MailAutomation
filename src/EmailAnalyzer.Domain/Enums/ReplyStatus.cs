namespace EmailAnalyzer.Domain.Enums;

/// <summary>
/// Lifecycle of a reply. A failed send stays Draft with LastSendError set — there is no
/// separate Failed state.
/// </summary>
public enum ReplyStatus
{
    Draft,
    Sent
}
