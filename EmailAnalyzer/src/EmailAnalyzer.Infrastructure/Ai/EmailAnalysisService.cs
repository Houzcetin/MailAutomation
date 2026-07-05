using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Orchestrates a single email analysis: truncate body, call the model, parse the JSON,
/// apply the confidence threshold. Never throws — on any API or parse failure it returns
/// a safe fallback (IsProcessed=false, RequiresHumanReview=true, category=Diger) and logs.
/// </summary>
public class EmailAnalysisService : IEmailAnalysisService
{
    private readonly OpenAiClient _client;
    private readonly AnalysisOptions _analysisOptions;
    private readonly ILogger<EmailAnalysisService> _logger;

    public EmailAnalysisService(
        OpenAiClient client,
        IOptions<AnalysisOptions> analysisOptions,
        ILogger<EmailAnalysisService> logger)
    {
        _client = client;
        _analysisOptions = analysisOptions.Value;
        _logger = logger;
    }

    public async Task<EmailAnalysisResult> AnalyzeAsync(
        string subject,
        string senderEmail,
        string? senderName,
        DateTime receivedDate,
        string body,
        CancellationToken cancellationToken = default)
    {
        var truncatedBody = Truncate(body, _analysisOptions.MaxBodyChars);
        var userPrompt = AiPromptBuilder.BuildUserPrompt(
            subject, senderEmail, senderName, receivedDate, truncatedBody);

        try
        {
            var raw = await _client.CompleteJsonAsync(
                AiPromptBuilder.SystemPrompt, userPrompt, cancellationToken);

            var result = AiResponseParser.Parse(raw);

            // Application-side rule: low confidence forces human review even if the
            // model reported false.
            if (result.Confidence < _analysisOptions.ConfidenceThreshold)
            {
                result.RequiresHumanReview = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            // Pipeline must not stop: log and return a safe fallback so the mail is still saved.
            _logger.LogError(ex,
                "Email analysis failed for subject '{Subject}' from {Sender}.", subject, senderEmail);

            return new EmailAnalysisResult
            {
                MainCategory = MainCategory.Diger,
                SubCategory = string.Empty,
                Priority = Priority.Low,
                Sentiment = Sentiment.Neutral,
                Summary = string.Empty,
                Confidence = 0m,
                RequiresHumanReview = true,
                IsProcessed = false,
                RawResponse = ex.Message
            };
        }
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxChars);
    }
}
