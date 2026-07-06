using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Infrastructure.Ai;
using Xunit;

namespace EmailAnalyzer.Tests;

public class ReplyPromptBuilderTests
{
    private static EmailMessage SampleEmail() => new()
    {
        Id = 1,
        SenderEmail = "musteri@firma.com",
        SenderName = "Ahmet Yılmaz",
        Subject = "E-fatura gönderim hatası",
        Body = "E-fatura kesmeye çalışıyoruz ama sistem hata veriyor.",
        ReceivedDate = new DateTime(2026, 7, 1, 10, 30, 0, DateTimeKind.Utc),
        MainCategory = MainCategory.EDonusum,
        SubCategory = "E-fatura gönderim hatası",
        Priority = Priority.High,
        Sentiment = Sentiment.Urgent,
        Summary = "E-fatura kesilemiyor, acil destek isteniyor."
    };

    [Fact]
    public void SystemPrompt_ContainsJsonContractAndLanguageRule()
    {
        Assert.Contains("replyBody", ReplyPromptBuilder.SystemPrompt);
        Assert.Contains("AYNI dilde", ReplyPromptBuilder.SystemPrompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsEmailAndAnalysisFields()
    {
        var email = SampleEmail();

        var prompt = ReplyPromptBuilder.BuildUserPrompt(
            email, email.Body, companyName: "Acme Yazılım", signOffName: "Zeynep Kaya");

        Assert.Contains("Ahmet Yılmaz", prompt);
        Assert.Contains("musteri@firma.com", prompt);
        Assert.Contains("E-fatura gönderim hatası", prompt);
        Assert.Contains("sistem hata veriyor", prompt);
        Assert.Contains("EDonusum", prompt);
        Assert.Contains("High", prompt);
        Assert.Contains("Urgent", prompt);
        Assert.Contains("E-fatura kesilemiyor", prompt);
        Assert.Contains("Acme Yazılım", prompt);
        Assert.Contains("Zeynep Kaya", prompt);
    }

    [Fact]
    public void BuildUserPrompt_UsesTruncatedBodyNotEntityBody()
    {
        var email = SampleEmail();

        var prompt = ReplyPromptBuilder.BuildUserPrompt(
            email, "KISALTILMIŞ GÖVDE", companyName: "", signOffName: "");

        Assert.Contains("KISALTILMIŞ GÖVDE", prompt);
        Assert.DoesNotContain("sistem hata veriyor", prompt);
    }

    [Fact]
    public void BuildUserPrompt_MissingOptionalFields_RenderedAsYok()
    {
        var email = SampleEmail();
        email.SenderName = null;
        email.SubCategory = string.Empty;
        email.Summary = string.Empty;

        var prompt = ReplyPromptBuilder.BuildUserPrompt(
            email, email.Body, companyName: "", signOffName: "");

        Assert.Contains("(yok)", prompt);
        Assert.DoesNotContain("Ahmet Yılmaz", prompt);
    }
}
