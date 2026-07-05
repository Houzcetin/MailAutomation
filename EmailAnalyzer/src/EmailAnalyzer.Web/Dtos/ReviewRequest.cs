using EmailAnalyzer.Domain.Enums;

namespace EmailAnalyzer.Web.Dtos;

/// <summary>
/// Human correction of an email's classification (PUT /api/emails/{id}/review). Only the
/// provided fields are applied; RequiresHumanReview is cleared regardless.
/// </summary>
public class ReviewRequest
{
    public MainCategory? MainCategory { get; set; }

    public string? SubCategory { get; set; }

    public Priority? Priority { get; set; }
}
