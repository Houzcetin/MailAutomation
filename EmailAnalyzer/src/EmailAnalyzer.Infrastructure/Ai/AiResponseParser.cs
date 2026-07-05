using System.Text.Json;
using System.Text.Json.Serialization;
using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Parses the model's raw response text into an <see cref="EmailAnalysisResult"/>.
/// Robust to leading/trailing prose or code fences: takes the substring from the first
/// '{' to the last '}' before deserializing. Unknown enum values fall back to safe
/// defaults. Kept static and dependency-free so it can be unit-tested in isolation.
/// </summary>
public static class AiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Parses the raw content; throws <see cref="FormatException"/> on failure.</summary>
    public static EmailAnalysisResult Parse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new FormatException("Empty AI response.");
        }

        var json = ExtractJsonObject(rawContent);

        AiResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AiResponseDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("AI response is not valid JSON.", ex);
        }

        if (dto is null)
        {
            throw new FormatException("AI response deserialized to null.");
        }

        return new EmailAnalysisResult
        {
            MainCategory = ParseEnum(dto.MainCategory, MainCategory.Diger),
            SubCategory = dto.SubCategory?.Trim() ?? string.Empty,
            Priority = ParseEnum(dto.Priority, Priority.Low),
            Sentiment = ParseEnum(dto.Sentiment, Sentiment.Neutral),
            Summary = dto.Summary?.Trim() ?? string.Empty,
            Confidence = Clamp01(dto.Confidence),
            RequiresHumanReview = dto.RequiresHumanReview,
            IsProcessed = true,
            RawResponse = rawContent
        };
    }

    /// <summary>Returns the substring from the first '{' to the last '}'.</summary>
    private static string ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start)
        {
            throw new FormatException("No JSON object found in AI response.");
        }

        return raw.Substring(start, end - start + 1);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static decimal Clamp01(decimal value) => value switch
    {
        < 0m => 0m,
        > 1m => 1m,
        _ => value
    };

    private sealed class AiResponseDto
    {
        [JsonPropertyName("mainCategory")]
        public string? MainCategory { get; set; }

        [JsonPropertyName("subCategory")]
        public string? SubCategory { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("sentiment")]
        public string? Sentiment { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }

        [JsonPropertyName("requiresHumanReview")]
        public bool RequiresHumanReview { get; set; }
    }
}
