using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Configuration;
using EmailAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Web.Workers;

/// <summary>
/// Background worker that polls the mailbox for unseen messages, analyses each with the AI
/// service, persists the result, and marks the mail as seen. Idempotent: a message already
/// in the database is skipped. One mail failing never stops the loop (spec section 7).
/// </summary>
public class EmailPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GmailOptions _gmailOptions;
    private readonly ILogger<EmailPollingService> _logger;

    public EmailPollingService(
        IServiceScopeFactory scopeFactory,
        IOptions<GmailOptions> gmailOptions,
        ILogger<EmailPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _gmailOptions = gmailOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _gmailOptions.PollingIntervalSeconds));
        _logger.LogInformation(
            "Email polling worker started. Interval: {Seconds}s, folder: {Folder}.",
            interval.TotalSeconds, _gmailOptions.Folder);

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A whole-cycle failure (e.g. IMAP connect) must not kill the worker.
                _logger.LogError(ex, "Email polling cycle failed. Will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // No mailbox configured yet — no-op so the app runs without Gmail credentials.
        if (string.IsNullOrWhiteSpace(_gmailOptions.Email))
        {
            _logger.LogWarning("Gmail is not configured (Email is empty). Skipping poll cycle.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var fetchService = scope.ServiceProvider.GetRequiredService<IEmailFetchService>();
        var analysisService = scope.ServiceProvider.GetRequiredService<IEmailAnalysisService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Load (or create) this folder's watermark so only mail newer than the last processed
        // UID is fetched. On the very first run there is no state → afterUid is null and the
        // fetch service baselines on the newest few messages instead of the whole backlog.
        var state = await dbContext.MailboxStates
            .FirstOrDefaultAsync(s => s.Folder == _gmailOptions.Folder, cancellationToken);

        uint? afterUid = state?.LastProcessedUid;

        _logger.LogInformation(
            "Polling mailbox {Email}, folder {Folder} (afterUid={AfterUid})...",
            _gmailOptions.Email, _gmailOptions.Folder, afterUid);

        var fetch = await fetchService.FetchAsync(afterUid, cancellationToken);

        // A changed UIDVALIDITY means the server's UID space was reset — our watermark is stale
        // and must be re-baselined. Discard this cycle's mail (it was fetched against the old
        // watermark) and store the new UIDVALIDITY + highest UID so next cycle starts clean.
        if (state is not null && state.UidValidity != fetch.UidValidity)
        {
            _logger.LogWarning(
                "UIDVALIDITY changed for {Folder} ({Old} → {New}). Re-baselining watermark.",
                _gmailOptions.Folder, state.UidValidity, fetch.UidValidity);
            state.UidValidity = fetch.UidValidity;
            state.LastProcessedUid = fetch.HighestUid;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (fetch.Emails.Count == 0)
        {
            _logger.LogInformation("No new mail this cycle.");
            // Still persist a baseline row on first run so we don't reprocess the backlog next time.
            await UpsertWatermarkAsync(dbContext, state, fetch.UidValidity, fetch.HighestUid, cancellationToken);
            return;
        }

        _logger.LogInformation("Fetched {Count} new email(s).", fetch.Emails.Count);

        uint highestProcessedUid = afterUid ?? 0;

        foreach (var email in fetch.Emails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Idempotency: never process the same Message-Id twice (guards against a UID range
                // that overlaps an already-stored mail, e.g. after a re-baseline).
                var alreadyExists = await dbContext.EmailMessages
                    .AnyAsync(e => e.MessageId == email.MessageId, cancellationToken);

                if (!alreadyExists)
                {
                    var result = await analysisService.AnalyzeAsync(
                        email.Subject, email.SenderEmail, email.SenderName,
                        email.ReceivedDate, email.Body, cancellationToken);

                    var entity = EmailMessage.FromAnalysis(
                        email.MessageId, email.SenderEmail, email.SenderName,
                        email.Subject, email.Body, email.ReceivedDate, result);

                    dbContext.EmailMessages.Add(entity);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Processed email UID {Uid} {MessageId} from {Sender} → {Category}/{Priority}.",
                        email.Uid, email.MessageId, email.SenderEmail, result.MainCategory, result.Priority);
                }
                else
                {
                    _logger.LogDebug("Message {MessageId} already processed, skipping.", email.MessageId);
                }

                // Advance the watermark only for mail we successfully handled, so a mid-batch
                // failure leaves the rest to be retried next cycle rather than being skipped.
                if (email.Uid > highestProcessedUid)
                {
                    highestProcessedUid = email.Uid;
                }
            }
            catch (Exception ex)
            {
                // One bad mail must not stop the rest of the batch. Stop advancing the watermark
                // here so this UID is retried next cycle.
                _logger.LogError(ex,
                    "Failed to process email UID {Uid} {MessageId} from {Sender}.",
                    email.Uid, email.MessageId, email.SenderEmail);
                break;
            }
        }

        await UpsertWatermarkAsync(dbContext, state, fetch.UidValidity, highestProcessedUid, cancellationToken);
    }

    /// <summary>Creates or updates the folder's watermark row.</summary>
    private async Task UpsertWatermarkAsync(
        AppDbContext dbContext,
        MailboxState? state,
        uint uidValidity,
        uint lastProcessedUid,
        CancellationToken cancellationToken)
    {
        if (state is null)
        {
            state = new MailboxState
            {
                Folder = _gmailOptions.Folder,
                UidValidity = uidValidity,
                LastProcessedUid = lastProcessedUid
            };
            dbContext.MailboxStates.Add(state);
        }
        else if (lastProcessedUid > state.LastProcessedUid || state.UidValidity != uidValidity)
        {
            state.UidValidity = uidValidity;
            state.LastProcessedUid = lastProcessedUid;
        }
        else
        {
            return; // nothing changed
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogDebug(
            "Watermark for {Folder} → UID {Uid} (UIDVALIDITY {UidValidity}).",
            _gmailOptions.Folder, lastProcessedUid, uidValidity);
    }
}
