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

        _logger.LogInformation("Polling mailbox {Email} for unseen mail...", _gmailOptions.Email);

        var emails = await fetchService.FetchUnseenAsync(cancellationToken);
        if (emails.Count == 0)
        {
            _logger.LogInformation("No unseen mail this cycle.");
            return;
        }

        _logger.LogInformation("Fetched {Count} unseen email(s).", emails.Count);

        foreach (var email in emails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Idempotency: never process the same Message-Id twice.
                var alreadyExists = await dbContext.EmailMessages
                    .AnyAsync(e => e.MessageId == email.MessageId, cancellationToken);

                if (alreadyExists)
                {
                    _logger.LogDebug("Message {MessageId} already processed, skipping.", email.MessageId);
                    await fetchService.MarkAsSeenAsync(email.MessageId, cancellationToken);
                    continue;
                }

                var result = await analysisService.AnalyzeAsync(
                    email.Subject, email.SenderEmail, email.SenderName,
                    email.ReceivedDate, email.Body, cancellationToken);

                var entity = EmailMessage.FromAnalysis(
                    email.MessageId, email.SenderEmail, email.SenderName,
                    email.Subject, email.Body, email.ReceivedDate, result);

                dbContext.EmailMessages.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);

                await fetchService.MarkAsSeenAsync(email.MessageId, cancellationToken);

                _logger.LogInformation(
                    "Processed email {MessageId} from {Sender} → {Category}/{Priority}.",
                    email.MessageId, email.SenderEmail, result.MainCategory, result.Priority);
            }
            catch (Exception ex)
            {
                // One bad mail must not stop the rest of the batch.
                _logger.LogError(ex,
                    "Failed to process email {MessageId} from {Sender}.",
                    email.MessageId, email.SenderEmail);
            }
        }
    }
}
