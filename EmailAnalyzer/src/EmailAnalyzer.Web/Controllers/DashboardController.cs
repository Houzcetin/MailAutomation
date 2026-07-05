using EmailAnalyzer.Infrastructure.Persistence;
using EmailAnalyzer.Web.Common;
using EmailAnalyzer.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Web.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DashboardController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Aggregated figures for the admin dashboard (spec section 8).</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats(CancellationToken cancellationToken = default)
    {
        var emails = _dbContext.EmailMessages.AsNoTracking();

        var totalCount = await emails.CountAsync(cancellationToken);
        var reviewCount = await emails.CountAsync(e => e.RequiresHumanReview, cancellationToken);

        var byCategory = await emails
            .GroupBy(e => e.MainCategory)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPriority = await emails
            .GroupBy(e => e.Priority)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var bySentiment = await emails
            .GroupBy(e => e.Sentiment)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Last 30 days of received-mail volume. Pull the window then group by day in memory
        // (30 days of rows is small, and this avoids DB-specific date-truncation translation).
        var since = DateTime.UtcNow.Date.AddDays(-29);
        var recentDates = await emails
            .Where(e => e.ReceivedDate >= since)
            .Select(e => e.ReceivedDate)
            .ToListAsync(cancellationToken);

        var last30Days = recentDates
            .GroupBy(d => d.Date)
            .Select(g => new DailyCount { Date = g.Key, Count = g.Count() })
            .OrderBy(d => d.Date)
            .ToList();

        var stats = new DashboardStatsDto
        {
            TotalCount = totalCount,
            RequiresHumanReviewCount = reviewCount,
            ByCategory = byCategory
                .Select(x => new CategoryCount
                {
                    Key = x.Key.ToString(),
                    Display = DisplayNames.For(x.Key),
                    Count = x.Count
                })
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByPriority = byPriority
                .Select(x => new LabelCount
                {
                    Key = x.Key.ToString(),
                    Display = DisplayNames.For(x.Key),
                    Count = x.Count
                })
                .ToList(),
            BySentiment = bySentiment
                .Select(x => new LabelCount
                {
                    Key = x.Key.ToString(),
                    Display = DisplayNames.For(x.Key),
                    Count = x.Count
                })
                .ToList(),
            Last30Days = last30Days
        };

        return Ok(stats);
    }
}
