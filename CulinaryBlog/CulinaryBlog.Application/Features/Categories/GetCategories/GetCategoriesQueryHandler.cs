using System.Text.Json;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CulinaryBlog.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Cache key dùng để invalidate khi Category được tạo, cập nhật hoặc xóa.
    /// Inject IDistributedCache vào Create/Update/Delete handler và gọi InvalidateCacheAsync().
    /// </summary>
    public const string CacheKey = "categories:all";

    public GetCategoriesQueryHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var recipeListVersion = await _context.GetRecipeListVersionAsync(cancellationToken);
        var cachedCategories = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedCategories))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CachedCategories>(cachedCategories);
                if (cached is not null && cached.RecipeListVersion == recipeListVersion)
                    return cached.Items;
            }
            catch (JsonException) { /* Old cache format; refresh it below. */ }
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                RecipeCount = c.Recipes.Count(r => r.Status == RecipeStatus.Published)
            })
            .ToListAsync(cancellationToken);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };

        await _cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(new CachedCategories(recipeListVersion, categories)),
            cacheOptions,
            cancellationToken);

        return categories;
    }

    /// <summary>
    /// Xóa cache danh sách category. Gọi method này trong handler Create/Update/Delete Category.
    /// Ví dụ: await GetCategoriesQueryHandler.InvalidateCacheAsync(cache, cancellationToken);
    /// </summary>
    public static Task InvalidateCacheAsync(IDistributedCache cache, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(CacheKey, cancellationToken);

    private sealed record CachedCategories(string? RecipeListVersion, List<CategoryDto> Items);
}
