using EmailAnalyzer.Infrastructure.Mail;
using Xunit;

namespace EmailAnalyzer.Tests;

public class ReplySubjectTests
{
    [Theory]
    [InlineData("E-fatura hatası", "Re: E-fatura hatası")]
    [InlineData("  Boşluklu konu  ", "Re: Boşluklu konu")]
    [InlineData("", "Re: ")]
    public void BuildReplySubject_PrependsRe(string subject, string expected)
    {
        Assert.Equal(expected, MailKitEmailReplySender.BuildReplySubject(subject));
    }

    [Theory]
    [InlineData("Re: E-fatura hatası")]
    [InlineData("RE: E-fatura hatası")]
    [InlineData("re: E-fatura hatası")]
    public void BuildReplySubject_ExistingRePrefix_NotDuplicated(string subject)
    {
        var result = MailKitEmailReplySender.BuildReplySubject(subject);

        Assert.Equal(subject, result);
        Assert.StartsWith("R", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("musteri@firma.com", true)]
    [InlineData("a@b.c", true)]
    [InlineData("manual-test@local", false)]
    [InlineData("@firma.com", false)]
    [InlineData("adressiz", false)]
    [InlineData("biten@", false)]
    public void IsRoutableAddress_RequiresDottedDomain(string address, bool expected)
    {
        Assert.Equal(expected, MailKitEmailReplySender.IsRoutableAddress(address));
    }
}
