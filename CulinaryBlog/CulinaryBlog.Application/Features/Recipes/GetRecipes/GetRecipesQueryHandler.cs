using System.Text.Json;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CulinaryBlog.Application.Features.Recipes.GetRecipes;

public class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetRecipesQueryHandler> _logger;

    public const string CacheKeyPrefix = "recipes:list:";

    public GetRecipesQueryHandler(IApplicationDbContext context, IDistributedCache cache,
        ILogger<GetRecipesQueryHandler>? logger = null)
    {
        _context = context;
        _cache = cache;
        _logger = logger ?? NullLogger<GetRecipesQueryHandler>.Instance;
    }

    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        GetRecipesQuery request,
        CancellationToken cancellationToken)
    {
        await new GetRecipesQueryValidator().ValidateAndThrowAsync(request, cancellationToken);
        bool shouldCache = string.IsNullOrEmpty(request.CurrentUserId) && !request.IsAdmin;
        var version = shouldCache ? await _context.GetRecipeListVersionAsync(cancellationToken) : null;
        shouldCache &= version is not null;
        var cacheKey = $"{BuildCacheKey(request)}:v{version}";
        
        if (shouldCache)
        {
            try
            {
                var cachedJson = await _cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cached = JsonSerializer.Deserialize<PagedResult<RecipeSummaryDto>>(cachedJson);
                    if (cached is not null) return cached;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Recipe list cache read failed; querying the database.");
                shouldCache = false;
            }
        }

        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Category)
            .AsQueryable();

        if (!request.IsAdmin)
        {
            if (!string.IsNullOrEmpty(request.CurrentUserId))
            {
                query = query.Where(r =>
                    r.Status == RecipeStatus.Published ||
                    (r.Status != RecipeStatus.Published && r.AuthorId == request.CurrentUserId));
            }
            else
            {
                query = query.Where(r => r.Status == RecipeStatus.Published);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(r => 
                r.Title.ToLower().Contains(keyword) || 
                (r.Description != null && r.Description.ToLower().Contains(keyword)));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(r => r.CategoryId == request.CategoryId.Value);

        if (request.Difficulty.HasValue)
            query = query.Where(r => r.Difficulty == request.Difficulty.Value);

        if (request.MaxCookTime.HasValue)
            query = query.Where(r => r.CookingTimeMinutes <= request.MaxCookTime.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = request.Sort switch
        {
            "createdAt"    => query.OrderBy(r => r.CreatedAt),
            "-createdAt"   => query.OrderByDescending(r => r.CreatedAt),
            "title"        => query.OrderBy(r => r.Title),
            "-title"       => query.OrderByDescending(r => r.Title),
            "cookTime"     => query.OrderBy(r => r.CookingTimeMinutes),
            "-cookTime"    => query.OrderByDescending(r => r.CookingTimeMinutes),
            "publishedAt"  => query.OrderBy(r => r.PublishedAt),
            "-publishedAt" => query.OrderByDescending(r => r.PublishedAt),
            _              => query.OrderByDescending(r => r.CreatedAt)
        };

        // A unique tie-breaker keeps equal sort values on consistent pages.
        // CountAsync returns an int, so offsets above int.MaxValue are always empty.
        var skip = (int)Math.Min((long)(request.Page - 1) * request.PageSize, int.MaxValue);

        var items = await orderedQuery
            .ThenBy(r => r.Id)
            .Skip(skip)
            .Take(request.PageSize)
            .Select(r => new RecipeSummaryDto
            {
                Id                 = r.Id,
                Title              = r.Title,
                Slug               = r.Slug,
                Description        = r.Description,
                ImageUrl           = r.ImageUrl,
                PrepTimeMinutes    = r.PrepTimeMinutes,
                CookingTimeMinutes = r.CookingTimeMinutes,
                Servings           = r.Servings,
                Difficulty         = r.Difficulty.ToString(),
                Status             = r.Status.ToString(),
                CategoryId         = r.CategoryId,
                CategoryName       = r.Category.Name,
                AuthorId           = r.AuthorId,
                CreatedAt          = r.CreatedAt,
                UpdatedAt          = r.UpdatedAt,
                PublishedAt        = r.PublishedAt
            })
            .ToListAsync(cancellationToken);

        var result = PagedResult<RecipeSummaryDto>.Create(items, totalCount, request.Page, request.PageSize);

        if (shouldCache)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            try
            {
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(result),
                    cacheOptions,
                    cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Recipe list cache write failed; returning database results.");
            }
        }

        return result;
    }

    private static string BuildCacheKey(GetRecipesQuery q)
    {
        var userId = q.IsAdmin ? "admin"
            : !string.IsNullOrEmpty(q.CurrentUserId) ? $"u:{q.CurrentUserId}"
            : "anon";

        return $"{CacheKeyPrefix}p{q.Page}_ps{q.PageSize}" +
               $"_kw{q.Keyword}" +
               $"_cid{q.CategoryId}" +
               $"_{q.Difficulty}" +
               $"_max{q.MaxCookTime}" +
               $"_{q.Sort}" +
               $"_{userId}";
    }

    public static string GetCacheKeyPrefix() => CacheKeyPrefix;
}
