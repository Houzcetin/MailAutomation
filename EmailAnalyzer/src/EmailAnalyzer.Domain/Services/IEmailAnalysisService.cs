using EmailAnalyzer.Domain.Dtos;

namespace EmailAnalyzer.Domain.Services;

/// <summary>Analyses an email with the AI model and returns a structured result.</summary>
public interface IEmailAnalysisService
{
    Task<EmailAnalysisResult> AnalyzeAsync(
        string subject,
        string senderEmail,
        string? senderName,
        DateTime receivedDate,
        string body,
        CancellationToken cancellationToken = default);
}
