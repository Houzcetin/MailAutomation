using EmailAnalyzer.Domain.Dtos;
using EmailAnalyzer.Domain.Entities;

namespace EmailAnalyzer.Domain.Services;

/// <summary>
/// Drafts a reply to an analysed email with the AI model. Takes the entity because the
/// draft depends on both the raw mail and its analysis (category, sentiment, summary).
/// </summary>
public interface IReplyGenerationService
{
    Task<ReplyGenerationResult> GenerateReplyAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default);
}
