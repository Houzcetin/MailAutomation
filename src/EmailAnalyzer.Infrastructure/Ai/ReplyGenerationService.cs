using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Orchestrates a single reply draft: truncate body, call the model with reply sampling
/// parameters, parse the {"replyBody": ...} envelope. Never throws — on any API or parse
/// failure it returns Success=false and logs, so the controller can shape the HTTP error.
/// </summary>
public class ReplyGenerationService : IReplyGenerationService
{
    private readonly OpenAiClient _client;
    private readonly ReplyOptions _replyOptions;
    private readonly AnalysisOptions _analysisOptions;
    private readonly ILogger<ReplyGenerationService> _logger;

    public ReplyGenerationService(
        OpenAiClient client,
        IOptions<ReplyOptions> replyOptions,
        IOptions<AnalysisOptions> analysisOptions,
        ILogger<ReplyGenerationService> logger)
    {
        _client = client;
        _replyOptions = replyOptions.Value;
        _analysisOptions = analysisOptions.Value;
        _logger = logger;
    }

    public async Task<ReplyGenerationResult> GenerateReplyAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default)
    {
        var truncatedBody = Truncate(email.Body, _analysisOptions.MaxBodyChars);
        var userPrompt = ReplyPromptBuilder.BuildUserPrompt(
            email, truncatedBody, _replyOptions.CompanyName, _replyOptions.SignOffName);

        try
        {
            var raw = await _client.CompleteJsonAsync(
                ReplyPromptBuilder.SystemPrompt,
                userPrompt,
                _replyOptions.Temperature,
                _replyOptions.MaxTokens,
                cancellationToken);

            return new ReplyGenerationResult
            {
                Success = true,
                ReplyBody = ReplyResponseParser.Parse(raw),
                RawResponse = raw
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Reply generation failed for email {EmailId} ('{Subject}').", email.Id, email.Subject);

            return new ReplyGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
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
