using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

// Applied states have already passed through AutomaticRetry's state election.
public sealed class WelcomeEmailFailureFilter(IServiceScopeFactory scopes,
    ILogger<WelcomeEmailFailureFilter> logger) : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState || context.BackgroundJob.Job.Type != typeof(WelcomeEmailJob)) return;
        var outboxId = (Guid)context.BackgroundJob.Job.Args[0];
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var changed = db.WelcomeEmailOutboxes.Where(x => x.Id == outboxId && x.Status != WelcomeEmailStatus.Sent)
                .ExecuteUpdate(update => update.SetProperty(x => x.Status, WelcomeEmailStatus.Failed));
            if (changed != 0) logger.LogError("Welcome email exhausted retries for outbox {OutboxId}, job {JobId}", outboxId, context.BackgroundJob.Id);
        }
        catch (Exception)
        {
            // Reconciliation repairs this if the application database was unavailable.
            logger.LogWarning("Deferred failed email state update for outbox {OutboxId}, job {JobId}", outboxId, context.BackgroundJob.Id);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}
