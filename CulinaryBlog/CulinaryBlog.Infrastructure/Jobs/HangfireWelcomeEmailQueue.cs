using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

// Separate scopes keep post-commit dispatch independent of the registration change tracker.
public sealed class HangfireWelcomeEmailQueue(IServiceScopeFactory scopes) : IWelcomeEmailQueue
{
    public async Task EnqueueAsync(Guid outboxId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"WelcomeEmailOutbox\" WHERE \"Id\" = {outboxId} FOR UPDATE", ct);
        var message = await db.WelcomeEmailOutboxes.SingleOrDefaultAsync(x => x.Id == outboxId, ct);
        if (message is null || message.Status != WelcomeEmailStatus.Pending) return;
        var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        message.HangfireJobId = jobs.Enqueue<WelcomeEmailJob>(job => job.ExecuteAsync(outboxId, CancellationToken.None));
        message.Status = WelcomeEmailStatus.Queued;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        scope.ServiceProvider.GetRequiredService<ILogger<HangfireWelcomeEmailQueue>>()
            .LogInformation("Queued welcome email {OutboxId} for user {UserId}, job {JobId}", message.Id, message.UserId, message.HangfireJobId);
    }
}
