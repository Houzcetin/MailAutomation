namespace EmailAnalyzer.Web.Dtos;

/// <summary>Aggregated figures for the admin dashboard (spec section 8).</summary>
public class DashboardStatsDto
{
    public int TotalCount { get; set; }

    public int RequiresHumanReviewCount { get; set; }

    /// <summary>Count per MainCategory (key = enum name, plus a Turkish label).</summary>
    public IReadOnlyList<CategoryCount> ByCategory { get; set; } = Array.Empty<CategoryCount>();

    /// <summary>Count per Priority.</summary>
    public IReadOnlyList<LabelCount> ByPriority { get; set; } = Array.Empty<LabelCount>();

    /// <summary>Count per Sentiment.</summary>
    public IReadOnlyList<LabelCount> BySentiment { get; set; } = Array.Empty<LabelCount>();

    /// <summary>Daily received-mail volume for the last 30 days.</summary>
    public IReadOnlyList<DailyCount> Last30Days { get; set; } = Array.Empty<DailyCount>();
}

public class CategoryCount
{
    public string Key { get; set; } = string.Empty;

    public string Display { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class LabelCount
{
    public string Key { get; set; } = string.Empty;

    public string Display { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class DailyCount
{
    public DateTime Date { get; set; }

    public int Count { get; set; }
}
