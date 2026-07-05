using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Persistence;
using EmailAnalyzer.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Web.Controllers;

[ApiController]
[Route("api/emails")]
public class EmailsController : ControllerBase
{
    private readonly IEmailAnalysisService _analysisService;
    private readonly AppDbContext _dbContext;

    public EmailsController(IEmailAnalysisService analysisService, AppDbContext dbContext)
    {
        _analysisService = analysisService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Manual, end-to-end analysis test. Runs the AI pipeline on the given subject/body
    /// (optional sender). With ?save=true the result is also persisted to the database.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        [FromBody] AnalyzeRequest request,
        [FromQuery] bool save = false,
        CancellationToken cancellationToken = default)
    {
        var senderEmail = string.IsNullOrWhiteSpace(request.SenderEmail)
            ? "manual-test@local"
            : request.SenderEmail;
        var receivedDate = DateTime.UtcNow;

        var result = await _analysisService.AnalyzeAsync(
            request.Subject, senderEmail, request.SenderName, receivedDate, request.Body, cancellationToken);

        var response = new AnalyzeResponse { Analysis = result };

        if (save)
        {
            var entity = EmailMessage.FromAnalysis(
                // Synthetic unique id for manual test records.
                messageId: $"manual-{Guid.NewGuid():N}",
                senderEmail: senderEmail,
                senderName: request.SenderName,
                subject: request.Subject,
                body: request.Body,
                receivedDate: receivedDate,
                result: result);

            _dbContext.EmailMessages.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            response.SavedId = entity.Id;
        }

        return Ok(response);
    }

    /// <summary>
    /// Paged, filtered list of analysed emails. Filters are optional and combined with AND.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmailListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] MainCategory? mainCategory = null,
        [FromQuery] Priority? priority = null,
        [FromQuery] Sentiment? sentiment = null,
        [FromQuery] bool? requiresHumanReview = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = _dbContext.EmailMessages.AsNoTracking().AsQueryable();

        if (mainCategory is not null)
        {
            query = query.Where(e => e.MainCategory == mainCategory);
        }

        if (priority is not null)
        {
            query = query.Where(e => e.Priority == priority);
        }

        if (sentiment is not null)
        {
            query = query.Where(e => e.Sentiment == sentiment);
        }

        if (requiresHumanReview is not null)
        {
            query = query.Where(e => e.RequiresHumanReview == requiresHumanReview);
        }

        if (dateFrom is not null)
        {
            query = query.Where(e => e.ReceivedDate >= dateFrom);
        }

        if (dateTo is not null)
        {
            query = query.Where(e => e.ReceivedDate <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.Subject.Contains(term) ||
                e.SenderEmail.Contains(term) ||
                e.Summary.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.ReceivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<EmailListItemDto>
        {
            Items = items.Select(EmailListItemDto.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    /// <summary>Full detail of a single email.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailDetailDto>> GetById(
        int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return Ok(EmailDetailDto.FromEntity(entity));
    }

    /// <summary>
    /// Applies a human correction: only the provided fields are updated, and the record is
    /// cleared of the human-review flag.
    /// </summary>
    [HttpPut("{id:int}/review")]
    public async Task<ActionResult<EmailDetailDto>> Review(
        int id,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmailMessages
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (request.MainCategory is not null)
        {
            entity.MainCategory = request.MainCategory.Value;
        }

        if (request.SubCategory is not null)
        {
            entity.SubCategory = request.SubCategory;
        }

        if (request.Priority is not null)
        {
            entity.Priority = request.Priority.Value;
        }

        entity.RequiresHumanReview = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(EmailDetailDto.FromEntity(entity));
    }
}
