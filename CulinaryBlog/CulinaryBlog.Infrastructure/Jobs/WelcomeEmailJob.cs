using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class WelcomeEmailJob(ApplicationDbContext db, IEmailSender sender,
    IOptions<SmtpOptions> options, ILogger<WelcomeEmailJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(Guid outboxId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"WelcomeEmailOutbox\" WHERE \"Id\" = {outboxId} FOR UPDATE", ct);
        var message = await db.WelcomeEmailOutboxes.SingleOrDefaultAsync(x => x.Id == outboxId, ct);
        if (message is null || message.Status == WelcomeEmailStatus.Sent) return;
        var (html, text) = WelcomeEmailTemplate.Render(message.FullName, options.Value.AppUrl);
        try
        {
            await sender.SendAsync(message.Email, WelcomeEmailTemplate.Subject, html, text, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Welcome email send failed for outbox {OutboxId}, user {UserId}, job {JobId}", message.Id, message.UserId, message.HangfireJobId);
            // SMTP exceptions can contain addresses or server responses. Keep them out of job storage/logs.
            throw new InvalidOperationException("Welcome email delivery failed; verify SMTP configuration and availability.");
        }
        message.SentAt = DateTime.UtcNow;
        message.Status = WelcomeEmailStatus.Sent;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Sent welcome email {OutboxId} for user {UserId}, job {JobId}", message.Id, message.UserId, message.HangfireJobId);
    }
}
