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
    private readonly IReplyGenerationService _replyGenerationService;
    private readonly IEmailReplySender _replySender;
    private readonly AppDbContext _dbContext;

    public EmailsController(
        IEmailAnalysisService analysisService,
        IReplyGenerationService replyGenerationService,
        IEmailReplySender replySender,
        AppDbContext dbContext)
    {
        _analysisService = analysisService;
        _replyGenerationService = replyGenerationService;
        _replySender = replySender;
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
            .Include(e => e.Reply)
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

    /// <summary>
    /// Drafts an AI reply for the email. Stateless: nothing is persisted — the user reviews
    /// the text in the editor and saves/sends explicitly.
    /// </summary>
    [HttpPost("{id:int}/reply/generate")]
    public async Task<ActionResult<GenerateReplyResponse>> GenerateReply(
        int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var result = await _replyGenerationService.GenerateReplyAsync(entity, cancellationToken);

        if (!result.Success)
        {
            // Detail is already logged by the service; the client gets a generic message.
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Yanıt oluşturulamadı. Lütfen tekrar deneyin." });
        }

        return Ok(new GenerateReplyResponse
        {
            ReplyBody = result.ReplyBody,
            RawResponse = result.RawResponse
        });
    }

    /// <summary>Saves (upserts) the reply draft. A sent reply is frozen.</summary>
    [HttpPut("{id:int}/reply")]
    public async Task<ActionResult<EmailReplyDto>> SaveReply(
        int id,
        [FromBody] SaveReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReplyBody))
        {
            return BadRequest(new { error = "Yanıt metni boş olamaz." });
        }

        var entity = await _dbContext.EmailMessages
            .Include(e => e.Reply)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (entity.Reply?.Status == ReplyStatus.Sent)
        {
            return Conflict(new { error = "Bu mail zaten yanıtlandı." });
        }

        var reply = UpsertDraft(entity, request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(EmailReplyDto.FromEntity(reply));
    }

    /// <summary>
    /// Sends the reply via SMTP. The draft is upserted with the posted body first, so what
    /// the user saw in the editor is exactly what gets persisted and sent. On SMTP failure
    /// the draft is kept and the error is stored on it.
    /// </summary>
    [HttpPost("{id:int}/reply/send")]
    public async Task<IActionResult> SendReply(
        int id,
        [FromBody] SaveReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReplyBody))
        {
            return BadRequest(new { error = "Yanıt metni boş olamaz." });
        }

        var entity = await _dbContext.EmailMessages
            .Include(e => e.Reply)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (entity.Reply?.Status == ReplyStatus.Sent)
        {
            return Conflict(new { error = "Bu mail zaten yanıtlandı." });
        }

        var reply = UpsertDraft(entity, request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await _replySender.SendReplyAsync(entity, request.ReplyBody, cancellationToken);

        if (!result.Success)
        {
            reply.LastSendError = Truncate(result.ErrorMessage ?? "Bilinmeyen hata", 2000);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = $"Gönderim başarısız: {result.ErrorMessage}" });
        }

        reply.Status = ReplyStatus.Sent;
        reply.SentDate = result.SentDate;
        reply.SentMessageId = result.SentMessageId;
        reply.LastSendError = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            status = ReplyStatus.Sent,
            sentDate = result.SentDate,
            sentTo = entity.SenderEmail
        });
    }

    /// <summary>Creates or updates the draft row with the posted editor content.</summary>
    private EmailReply UpsertDraft(EmailMessage entity, SaveReplyRequest request)
    {
        var now = DateTime.UtcNow;

        if (entity.Reply is null)
        {
            entity.Reply = new EmailReply
            {
                EmailMessageId = entity.Id,
                Body = request.ReplyBody,
                Status = ReplyStatus.Draft,
                IsAiGenerated = request.IsAiGenerated,
                AiRawResponse = request.AiRawResponse,
                CreatedDate = now,
                UpdatedDate = now
            };
            _dbContext.EmailReplies.Add(entity.Reply);
        }
        else
        {
            entity.Reply.Body = request.ReplyBody;
            entity.Reply.IsAiGenerated = request.IsAiGenerated;
            if (request.AiRawResponse is not null)
            {
                entity.Reply.AiRawResponse = request.AiRawResponse;
            }
            entity.Reply.UpdatedDate = now;
        }

        return entity.Reply;
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value.Substring(0, maxChars);
}
