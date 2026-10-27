using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.GetRecipes;

public class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRecipesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        GetRecipesQuery request,
        CancellationToken cancellationToken)
    {
        // Validation is owned by the MediatR ValidationBehavior.
        // Normalize once so ordering and cache identity have identical semantics.
        request = request with { Sort = NormalizeSort(request.Sort) };
        // Parse raw difficulty string into enum if provided and valid.
        var difficulty = request.Difficulty;
        if (!string.IsNullOrWhiteSpace(request.DifficultyRaw))
        {
            if (Enum.TryParse<CulinaryBlog.Domain.Entities.RecipeDifficulty>(request.DifficultyRaw, true, out var diffEnum))
                difficulty = diffEnum;
        }
        request = request with { Difficulty = difficulty };
        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Category)
            .AsQueryable();

        if (!request.IsAdmin)
        {
            if (!string.IsNullOrEmpty(request.CurrentUserId))
            {
                var isParsed = Guid.TryParse(request.CurrentUserId, out var parsedCurrentUserId);
                query = query.Where(r =>
                    r.Status == RecipeStatus.Published ||
                    (r.Status != RecipeStatus.Published && isParsed && r.AuthorId == parsedCurrentUserId));
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

        if (request.AuthorId.HasValue)
            query = query.Where(r => r.AuthorId == request.AuthorId.Value);

        if (!string.IsNullOrWhiteSpace(request.StatusRaw) &&
            Enum.TryParse<RecipeStatus>(request.StatusRaw.Trim(), true, out var status))
            query = query.Where(r => r.Status == status);

        if (request.Difficulty.HasValue)
            query = query.Where(r => r.Difficulty == request.Difficulty.Value);

        if (request.MaxCookTime.HasValue)
            query = query.Where(r => r.CookingTimeMinutes <= request.MaxCookTime.Value);

        if (request.MinServings.HasValue)
            query = query.Where(r => r.Servings >= request.MinServings.Value);

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
                AuthorId = r.AuthorId,
                CreatedAt          = r.CreatedAt,
                UpdatedAt          = r.UpdatedAt,
                PublishedAt        = r.PublishedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<RecipeSummaryDto>.Create(items, totalCount, request.Page, request.PageSize);
    }

    private static string NormalizeSort(string? sort) => sort?.Trim() switch
    {
        "createdAt" or "-createdAt" or "title" or "-title" or
        "cookTime" or "-cookTime" or "publishedAt" or "-publishedAt" => sort.Trim(),
        _ => "-createdAt"
    };

}
