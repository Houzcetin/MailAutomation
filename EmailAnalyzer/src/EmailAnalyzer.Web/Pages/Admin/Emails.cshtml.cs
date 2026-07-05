using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Infrastructure.Persistence;
using EmailAnalyzer.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Web.Pages.Admin;

public class EmailsModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public EmailsModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Filters (bound from query string).
    [BindProperty(SupportsGet = true)] public MainCategory? MainCategory { get; set; }
    [BindProperty(SupportsGet = true)] public Priority? Priority { get; set; }
    [BindProperty(SupportsGet = true)] public Sentiment? Sentiment { get; set; }
    [BindProperty(SupportsGet = true)] public bool? RequiresHumanReview { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public int PageSize { get; } = 20;
    public int TotalCount { get; private set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public IReadOnlyList<EmailListItemDto> Items { get; private set; } = Array.Empty<EmailListItemDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (PageNumber < 1) PageNumber = 1;

        var query = _dbContext.EmailMessages.AsNoTracking().AsQueryable();

        if (MainCategory is not null) query = query.Where(e => e.MainCategory == MainCategory);
        if (Priority is not null) query = query.Where(e => e.Priority == Priority);
        if (Sentiment is not null) query = query.Where(e => e.Sentiment == Sentiment);
        if (RequiresHumanReview is not null) query = query.Where(e => e.RequiresHumanReview == RequiresHumanReview);
        if (DateFrom is not null) query = query.Where(e => e.ReceivedDate >= DateFrom);
        if (DateTo is not null) query = query.Where(e => e.ReceivedDate <= DateTo);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(e =>
                e.Subject.Contains(term) ||
                e.SenderEmail.Contains(term) ||
                e.Summary.Contains(term));
        }

        TotalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(e => e.ReceivedDate)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        Items = entities.Select(EmailListItemDto.FromEntity).ToList();
    }

    /// <summary>Builds a query string preserving current filters, overriding the page number.</summary>
    public string PageLink(int page)
    {
        var q = new List<string> { $"page={page}" };
        if (MainCategory is not null) q.Add($"mainCategory={MainCategory}");
        if (Priority is not null) q.Add($"priority={Priority}");
        if (Sentiment is not null) q.Add($"sentiment={Sentiment}");
        if (RequiresHumanReview is not null) q.Add($"requiresHumanReview={RequiresHumanReview.Value.ToString().ToLowerInvariant()}");
        if (DateFrom is not null) q.Add($"dateFrom={DateFrom:yyyy-MM-dd}");
        if (DateTo is not null) q.Add($"dateTo={DateTo:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(Search)) q.Add($"search={Uri.EscapeDataString(Search)}");
        return "/admin/emails?" + string.Join("&", q);
    }
}
