using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace CulinaryBlog.API.Endpoints;

public sealed class RecipeImageCacheInvalidator(
    IApplicationDbContext db, IOutputCacheStore outputCache, IDistributedCache cache, ILogger<RecipeImageCacheInvalidator> logger)
{
    public async Task InvalidateAsync()
    {
        // A disconnected HTTP caller must not cancel invalidation of an already committed write.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await ProcessAsync(timeout.Token); }
        catch (Exception ex) { logger.LogError(ex, "Recipe cache invalidation is pending; worker will retry"); }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(CancellationToken ct)
    {
        var pending = await db.RecipeCacheInvalidations.Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        if (pending.Count == 0) return;
        await outputCache.EvictByTagAsync("recipes", ct);
        await cache.SetStringAsync(GetRecipesQueryHandler.CacheVersionKey, Guid.NewGuid().ToString("N"),
            new DistributedCacheEntryOptions(), ct);
        foreach (var entry in pending) entry.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
