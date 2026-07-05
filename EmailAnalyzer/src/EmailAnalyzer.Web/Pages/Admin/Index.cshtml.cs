using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public IndexModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int TotalCount { get; private set; }
    public int CriticalCount { get; private set; }
    public int ReviewCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var emails = _dbContext.EmailMessages.AsNoTracking();

        TotalCount = await emails.CountAsync(cancellationToken);
        CriticalCount = await emails.CountAsync(e => e.Priority == Priority.Critical, cancellationToken);
        ReviewCount = await emails.CountAsync(e => e.RequiresHumanReview, cancellationToken);
    }
}
