using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmailAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Thin raw-HTTP client over the OpenAI Chat Completions API. Uses JSON mode so the
/// model is constrained to return a single valid JSON object. Retries transient
/// failures (429, 5xx) with exponential backoff (2s / 4s / 8s).
/// </summary>
public class OpenAiClient
{
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends a system + user prompt and returns the raw assistant message content
    /// (choices[0].message.content). Throws on non-retryable errors or after retries
    /// are exhausted.
    /// </summary>
    public Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
        => CompleteJsonAsync(systemPrompt, userPrompt, temperature: 0, _options.MaxTokens, cancellationToken);

    /// <summary>
    /// Variant with explicit sampling parameters. Classification uses temperature 0;
    /// reply drafting uses a higher temperature for natural prose.
    /// </summary>
    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _options.Model,
            temperature,
            max_tokens = maxTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        for (var attempt = 0; ; attempt++)
        {
            HttpStatusCode statusCode;
            string errorBody;

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    "chat/completions", payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    return doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;
                }

                var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                                  (int)response.StatusCode >= 500;

                if (isTransient && attempt < RetryDelays.Length)
                {
                    var retryDelay = RetryDelays[attempt];
                    _logger.LogWarning(
                        "OpenAI request transient failure {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{Max}).",
                        (int)response.StatusCode, retryDelay.TotalSeconds, attempt + 1, RetryDelays.Length);
                    await Task.Delay(retryDelay, cancellationToken);
                    continue;
                }

                // Non-retryable status, or retries exhausted: capture and throw below
                // (outside the try so this throw is not caught as a connection failure).
                statusCode = response.StatusCode;
                errorBody = await SafeReadAsync(response, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < RetryDelays.Length)
            {
                // No HTTP response was produced (e.g. DNS resolution / socket failure).
                // Treat connection-level errors as transient and retry with the same backoff.
                var retryDelay = RetryDelays[attempt];
                _logger.LogWarning(
                    "OpenAI connection failure, retrying in {Delay}s (attempt {Attempt}/{Max}): {Reason}",
                    retryDelay.TotalSeconds, attempt + 1, RetryDelays.Length, ex.Message);
                await Task.Delay(retryDelay, cancellationToken);
                continue;
            }

            throw new HttpRequestException(
                $"OpenAI request failed ({(int)statusCode} {statusCode}): {errorBody}");
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return "<unreadable response body>";
        }
    }
}
