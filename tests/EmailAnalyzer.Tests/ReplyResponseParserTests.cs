using EmailAnalyzer.Infrastructure.Ai;
using Xunit;

namespace EmailAnalyzer.Tests;

public class ReplyResponseParserTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsBody()
    {
        var raw = """{"replyBody": "Sayın Ahmet Bey,\n\nTalebiniz alınmıştır."}""";

        var body = ReplyResponseParser.Parse(raw);

        Assert.Equal("Sayın Ahmet Bey,\n\nTalebiniz alınmıştır.", body);
    }

    [Fact]
    public void Parse_WithCodeFence_StripsAndParses()
    {
        var raw = "```json\n{\"replyBody\": \"Merhaba\"}\n```";

        Assert.Equal("Merhaba", ReplyResponseParser.Parse(raw));
    }

    [Fact]
    public void Parse_WithLeadingAndTrailingProse_ExtractsJson()
    {
        var raw = "İşte yanıt:\n{\"replyBody\": \"Merhaba\"}\nUmarım yardımcı olur.";

        Assert.Equal("Merhaba", ReplyResponseParser.Parse(raw));
    }

    [Fact]
    public void Parse_PreservesNewlines()
    {
        var raw = """{"replyBody": "Satır 1\nSatır 2"}""";

        var body = ReplyResponseParser.Parse(raw);

        Assert.Contains("\n", body);
        Assert.Equal("Satır 1\nSatır 2", body);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var raw = """{"replyBody": "  Merhaba  "}""";

        Assert.Equal("Merhaba", ReplyResponseParser.Parse(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespaceRaw_Throws(string raw)
    {
        Assert.Throws<FormatException>(() => ReplyResponseParser.Parse(raw));
    }

    [Fact]
    public void Parse_NoJsonObject_Throws()
    {
        Assert.Throws<FormatException>(() => ReplyResponseParser.Parse("sadece düz metin"));
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        Assert.Throws<FormatException>(() => ReplyResponseParser.Parse("{replyBody: bozuk}"));
    }

    [Fact]
    public void Parse_MissingReplyBody_Throws()
    {
        Assert.Throws<FormatException>(() => ReplyResponseParser.Parse("""{"other": "alan"}"""));
    }

    [Fact]
    public void Parse_EmptyReplyBody_Throws()
    {
        Assert.Throws<FormatException>(() => ReplyResponseParser.Parse("""{"replyBody": "  "}"""));
    }

    [Fact]
    public void Parse_CaseInsensitivePropertyName_Works()
    {
        Assert.Equal("Merhaba", ReplyResponseParser.Parse("""{"ReplyBody": "Merhaba"}"""));
    }
}
