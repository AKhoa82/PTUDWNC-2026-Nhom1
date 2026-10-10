using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class FileDeletionReconciliationJob(ApplicationDbContext db, IFileDeletionQueue queue,
    ILogger<FileDeletionReconciliationJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 240)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var expired = await db.StoredFiles.FromSqlRaw("""
                SELECT * FROM "StoredFiles"
                WHERE "Status" = 0 AND "UploadExpiresAt" <= now()
                ORDER BY "UploadExpiresAt" LIMIT 100 FOR UPDATE SKIP LOCKED
                """).ToListAsync(ct);
            foreach (var file in expired)
            {
                if (await db.RecipeImages.AnyAsync(x => x.StoredFileId == file.Id, ct))
                {
                    logger.LogError("Pending upload {StoredFileId} has a live image reference; preserving it", file.Id);
                    continue;
                }
                file.Status = StoredFileStatus.DeletePending;
                file.DeletionRequestedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        var pendingIds = await db.StoredFiles.AsNoTracking()
            .Where(file => file.Status == StoredFileStatus.DeletePending && file.DeletionRequestedAt != null && file.DeletedAt == null && file.DeletionJobId == null)
            .OrderBy(file => file.DeletionRequestedAt)
            .Select(file => file.Id)
            .Take(100)
            .ToListAsync(ct);
        foreach (var id in pendingIds)
        {
            ct.ThrowIfCancellationRequested();
            try { queue.Enqueue(id); }
            catch (Exception ex) { logger.LogError(ex, "Cannot enqueue pending deletion {StoredFileId}", id); }
        }
    }
}
