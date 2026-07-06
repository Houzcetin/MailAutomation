using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Parses the model's raw response into the reply body text. Expects a {"replyBody": "..."}
/// envelope; robust to leading/trailing prose or code fences (first '{' to last '}').
/// Kept static and dependency-free so it can be unit-tested in isolation.
/// </summary>
public static class ReplyResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Parses the raw content; throws <see cref="FormatException"/> on failure.</summary>
    public static string Parse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new FormatException("Empty AI response.");
        }

        var json = ExtractJsonObject(rawContent);

        ReplyDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ReplyDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("AI response is not valid JSON.", ex);
        }

        var body = dto?.ReplyBody?.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new FormatException("AI response contains no replyBody.");
        }

        return body;
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

    private sealed class ReplyDto
    {
        [JsonPropertyName("replyBody")]
        public string? ReplyBody { get; set; }
    }
}
