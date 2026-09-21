using System.Text.Json;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CulinaryBlog.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "categories:all";

    public GetCategoriesQueryHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        // 1. Đọc dữ liệu từ Redis cache
        var cachedCategories = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedCategories))
        {
            var result = JsonSerializer.Deserialize<List<CategoryDto>>(cachedCategories);
            if (result is not null)
            {
                return result;
            }
        }

        // 2. Query database: Map sang CategoryDto, sắp xếp Name ASC
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                RecipeCount = c.Recipes.Count
            })
            .ToListAsync(cancellationToken);

        // 3. Cache kết quả với TTL 60 phút
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };

        await _cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(categories),
            cacheOptions,
            cancellationToken);

        return categories;
    }
}