using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class HangfireImageResizeQueue(IBackgroundJobClient jobs, ApplicationDbContext db) : IImageResizeQueue
{
    public async Task<string> EnqueueAsync(Guid imageId, CancellationToken ct = default)
    {
        var jobId = jobs.Enqueue<ImageResizeJob>(
            job => job.ExecuteAsync(
                imageId,
                CancellationToken.None));
        await db.RecipeImages
            .Where(image => image.Id == imageId && image.ResizeJobId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(image => image.ResizeJobId, jobId), ct);
        return jobId;
    }
}
