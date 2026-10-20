using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class ImageResizeReconciliationJob(
    ApplicationDbContext db,
    IImageResizeQueue queue,
    ILogger<ImageResizeReconciliationJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 240)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var pendingIds = await db.RecipeImages.AsNoTracking()
            .Where(image => image.StoredFileId != null && image.ResizeJobId == null &&
                (image.MediumUrl == null || image.ThumbnailUrl == null) &&
                image.StoredFile != null && image.StoredFile.Status == StoredFileStatus.Active &&
                image.StoredFile.DeletedAt == null)
            .OrderBy(image => image.CreatedAt)
            .Select(image => image.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (var imageId in pendingIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await queue.EnqueueAsync(imageId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Could not enqueue resize for image {ImageId}; reconciliation will retry", imageId);
            }
        }
    }
}
