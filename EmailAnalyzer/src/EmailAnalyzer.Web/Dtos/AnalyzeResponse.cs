using EmailAnalyzer.Domain.Dtos;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>Result of POST /api/emails/analyze. <see cref="SavedId"/> is set only when save=true.</summary>
public class AnalyzeResponse
{
    public EmailAnalysisResult Analysis { get; set; } = new();

    public int? SavedId { get; set; }
}
