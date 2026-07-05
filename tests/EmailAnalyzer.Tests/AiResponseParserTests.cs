using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Infrastructure.Ai;
using Xunit;

namespace EmailAnalyzer.Tests;

public class AiResponseParserTests
{
    private const string ValidJson = """
        {
          "mainCategory": "MuhasebeFinans",
          "subCategory": "E-fatura gönderim hatası",
          "priority": "High",
          "sentiment": "Urgent",
          "summary": "E-fatura kesilemiyor, acil.",
          "confidence": 0.92,
          "requiresHumanReview": true
        }
        """;

    [Fact]
    public void Parse_ValidJson_MapsAllFields()
    {
        var result = AiResponseParser.Parse(ValidJson);

        Assert.Equal(MainCategory.MuhasebeFinans, result.MainCategory);
        Assert.Equal("E-fatura gönderim hatası", result.SubCategory);
        Assert.Equal(Priority.High, result.Priority);
        Assert.Equal(Sentiment.Urgent, result.Sentiment);
        Assert.Equal(0.92m, result.Confidence);
        Assert.True(result.RequiresHumanReview);
        Assert.True(result.IsProcessed);
    }

    [Fact]
    public void Parse_WithCodeFence_StripsAndParses()
    {
        var raw = "```json\n" + ValidJson + "\n```";

        var result = AiResponseParser.Parse(raw);

        Assert.Equal(MainCategory.MuhasebeFinans, result.MainCategory);
    }

    [Fact]
    public void Parse_WithLeadingAndTrailingProse_ExtractsJson()
    {
        var raw = "İşte analiz sonucu:\n" + ValidJson + "\nUmarım yardımcı olur.";

        var result = AiResponseParser.Parse(raw);

        Assert.Equal(Priority.High, result.Priority);
    }

    [Fact]
    public void Parse_UnknownEnumValues_FallBackToDefaults()
    {
        var raw = """
            {
              "mainCategory": "Uzay",
              "priority": "SüperAcil",
              "sentiment": "Mutlu",
              "confidence": 0.5
            }
            """;

        var result = AiResponseParser.Parse(raw);

        Assert.Equal(MainCategory.Diger, result.MainCategory);
        Assert.Equal(Priority.Low, result.Priority);
        Assert.Equal(Sentiment.Neutral, result.Sentiment);
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(-0.3, 0.0)]
    [InlineData(0.7, 0.7)]
    public void Parse_ConfidenceOutOfRange_IsClampedTo01(double input, double expected)
    {
        var raw = $$"""{ "mainCategory": "ERP", "confidence": {{input.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""";

        var result = AiResponseParser.Parse(raw);

        Assert.Equal((decimal)expected, result.Confidence);
    }

    [Fact]
    public void Parse_MissingOptionalFields_UsesEmptyDefaults()
    {
        var raw = """{ "mainCategory": "ERP" }""";

        var result = AiResponseParser.Parse(raw);

        Assert.Equal(MainCategory.ERP, result.MainCategory);
        Assert.Equal(string.Empty, result.SubCategory);
        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal(0m, result.Confidence);
        Assert.False(result.RequiresHumanReview);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Burada hiç JSON yok, sadece düz metin.")]
    public void Parse_NoJson_ThrowsFormatException(string raw)
    {
        Assert.Throws<FormatException>(() => AiResponseParser.Parse(raw));
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsFormatException()
    {
        var raw = """{ "mainCategory": "ERP", "priority": }""";

        Assert.Throws<FormatException>(() => AiResponseParser.Parse(raw));
    }
}
