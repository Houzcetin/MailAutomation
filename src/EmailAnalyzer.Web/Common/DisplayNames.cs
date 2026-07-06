using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Web.Common;

/// <summary>
/// Turkish display labels for the classification enums (spec section 5). The enum names stay
/// code-friendly (e.g. MuhasebeFinans); these are what the UI/API shows to the user.
/// </summary>
public static class DisplayNames
{
    private static readonly Dictionary<MainCategory, string> Categories = new()
    {
        [MainCategory.ERP] = "ERP",
        [MainCategory.MuhasebeFinans] = "Muhasebe / Finans",
        [MainCategory.EDonusum] = "E-Dönüşüm",
        [MainCategory.CRM] = "CRM",
        [MainCategory.IK] = "İK",
        [MainCategory.RaporlamaAnalitik] = "Raporlama / Analitik",
        [MainCategory.EgitimDanismanlik] = "Eğitim / Danışmanlık",
        [MainCategory.Diger] = "Diğer"
    };

    private static readonly Dictionary<Priority, string> Priorities = new()
    {
        [Priority.Low] = "Düşük",
        [Priority.Medium] = "Orta",
        [Priority.High] = "Yüksek",
        [Priority.Critical] = "Kritik"
    };

    private static readonly Dictionary<Sentiment, string> Sentiments = new()
    {
        [Sentiment.Positive] = "Olumlu",
        [Sentiment.Neutral] = "Nötr",
        [Sentiment.Negative] = "Olumsuz",
        [Sentiment.Angry] = "Öfkeli",
        [Sentiment.Urgent] = "Acil"
    };

    private static readonly Dictionary<ReplyStatus, string> ReplyStatuses = new()
    {
        [ReplyStatus.Draft] = "Taslak",
        [ReplyStatus.Sent] = "Gönderildi"
    };

    /// <summary>Bootstrap badge color class for a priority (spec section 9).</summary>
    public static string BadgeClass(Priority value) => value switch
    {
        Priority.Critical => "badge-danger",
        Priority.High => "badge-warning",
        Priority.Medium => "badge-info",
        Priority.Low => "badge-secondary",
        _ => "badge-secondary"
    };

    public static string For(MainCategory value) =>
        Categories.TryGetValue(value, out var label) ? label : value.ToString();

    public static string For(Priority value) =>
        Priorities.TryGetValue(value, out var label) ? label : value.ToString();

    public static string For(Sentiment value) =>
        Sentiments.TryGetValue(value, out var label) ? label : value.ToString();

    public static string For(ReplyStatus value) =>
        ReplyStatuses.TryGetValue(value, out var label) ? label : value.ToString();
}
