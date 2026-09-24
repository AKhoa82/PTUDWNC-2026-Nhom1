using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class HangfireFileDeletionQueue(IBackgroundJobClient jobs, IServiceScopeFactory scopes) : IFileDeletionQueue
{
    public string Enqueue(Guid storedFileId)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlInterpolated($"SELECT 1 FROM \"StoredFiles\" WHERE \"Id\" = {storedFileId} FOR UPDATE");
        var file = db.StoredFiles.SingleOrDefault(file => file.Id == storedFileId);
        if (file is null || file.Status != StoredFileStatus.DeletePending || file.DeletedAt != null) return string.Empty;
        // Retain Failed jobs for manual retry; reconciliation must not restart the retry budget.
        if (file.DeletionJobId != null) return file.DeletionJobId;
        file.DeletionJobId = jobs.Enqueue<DeleteStoredFileJob>(job => job.ExecuteAsync(storedFileId, CancellationToken.None));
        db.SaveChanges();
        transaction.Commit();
        return file.DeletionJobId;
    }
}
