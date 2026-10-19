using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class WelcomeEmailReconciliationJob(ApplicationDbContext db, IWelcomeEmailQueue queue,
    JobStorage storage, ILogger<WelcomeEmailReconciliationJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var pending = await db.WelcomeEmailOutboxes.AsNoTracking()
            .Where(x => x.Status == WelcomeEmailStatus.Pending).OrderBy(x => x.CreatedAt)
            .Select(x => x.Id).Take(100).ToListAsync(ct);
        foreach (var id in pending)
        {
            try { await queue.EnqueueAsync(id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { logger.LogWarning("Welcome email dispatch deferred for outbox {OutboxId}", id); }
        }
        // Also repairs a crash between Hangfire's final state transition and the outbox update.
        var queued = await db.WelcomeEmailOutboxes.AsNoTracking()
            .Where(x => x.Status == WelcomeEmailStatus.Queued && x.HangfireJobId != null)
            .Select(x => new { x.Id, x.HangfireJobId }).ToListAsync(ct);
        using var connection = storage.GetConnection();
        foreach (var message in queued)
        {
            ct.ThrowIfCancellationRequested();
            if (connection.GetStateData(message.HangfireJobId!)?.Name != "Failed") continue;
            var changed = await db.WelcomeEmailOutboxes
                .Where(x => x.Id == message.Id && x.Status == WelcomeEmailStatus.Queued)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, WelcomeEmailStatus.Failed), ct);
            if (changed != 0) logger.LogError("Welcome email exhausted retries for outbox {OutboxId}, job {JobId}", message.Id, message.HangfireJobId);
        }
    }
}
