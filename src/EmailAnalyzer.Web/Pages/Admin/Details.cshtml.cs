using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Infrastructure.Persistence;
using EmailAnalyzer.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Web.Pages.Admin;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public DetailsModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public EmailDetailDto Email { get; private set; } = new();

    // Review form fields.
    [BindProperty] public MainCategory ReviewMainCategory { get; set; }
    [BindProperty] public string? ReviewSubCategory { get; set; }
    [BindProperty] public Priority ReviewPriority { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        Email = EmailDetailDto.FromEntity(entity);
        ReviewMainCategory = entity.MainCategory;
        ReviewSubCategory = entity.SubCategory;
        ReviewPriority = entity.Priority;
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.EmailMessages
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.MainCategory = ReviewMainCategory;
        entity.SubCategory = ReviewSubCategory ?? string.Empty;
        entity.Priority = ReviewPriority;
        entity.RequiresHumanReview = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Post/Redirect/Get so a refresh doesn't resubmit.
        return RedirectToPage(new { id });
    }
}
