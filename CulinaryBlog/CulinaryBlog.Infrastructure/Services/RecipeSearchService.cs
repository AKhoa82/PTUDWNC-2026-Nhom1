using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class RecipeSearchService(ApplicationDbContext context) : IRecipeSearchService
{
    public async Task<PagedResult<RecipeSummaryDto>> SearchAsync(SearchRecipesQuery request, CancellationToken cancellationToken)
    {
        var terms = RecipeSearchTerms.ToPrefixQuery(request.SearchTerm);
        var query = context.Recipes.AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .Where(r => EF.Property<NpgsqlTsVector>(r, "SearchVector")
                .Matches(EF.Functions.ToTsQuery("public.vietnamese", terms)));

        if (request.CategoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrEmpty(request.Difficulty) && Enum.TryParse<RecipeDifficulty>(request.Difficulty, true, out var diff))
        {
            query = query.Where(r => r.Difficulty == diff);
        }

        if (request.MaxCookTime.HasValue)
        {
            query = query.Where(r => r.CookingTimeMinutes <= request.MaxCookTime.Value);
        }

        if (request.MinServings.HasValue)
        {
            query = query.Where(r => r.Servings >= request.MinServings.Value);
        }

        var count = await query.CountAsync(cancellationToken);

        var orderedQuery = request.Sort?.ToLowerInvariant() switch
        {
            "createdat" => query.OrderBy(r => r.CreatedAt),
            "-createdat" => query.OrderByDescending(r => r.CreatedAt),
            "title" => query.OrderBy(r => r.Title),
            "-title" => query.OrderByDescending(r => r.Title),
            "cooktime" => query.OrderBy(r => r.CookingTimeMinutes),
            "-cooktime" => query.OrderByDescending(r => r.CookingTimeMinutes),
            "publishedat" => query.OrderBy(r => r.PublishedAt),
            "-publishedat" => query.OrderByDescending(r => r.PublishedAt),
            _ => query.OrderByDescending(r => EF.Property<NpgsqlTsVector>(r, "SearchVector").Rank(EF.Functions.ToTsQuery("public.vietnamese", terms)))
        };

        var items = await orderedQuery
            .ThenBy(r => r.Id)
            .Skip((int)Math.Min((long)(request.Page - 1) * request.PageSize, int.MaxValue))
            .Take(request.PageSize)
            .Select(r => new RecipeSummaryDto
            {
                Id = r.Id, Title = r.Title, Slug = r.Slug, Description = r.Description,
                ImageUrl = r.ImageUrl, PrepTimeMinutes = r.PrepTimeMinutes,
                CookingTimeMinutes = r.CookingTimeMinutes, Servings = r.Servings,
                Difficulty = r.Difficulty.ToString(), Status = r.Status.ToString(),
                CategoryId = r.CategoryId, CategoryName = r.Category.Name, AuthorId = r.AuthorId,
                CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt, PublishedAt = r.PublishedAt,
                RelevanceScore = EF.Property<NpgsqlTsVector>(r, "SearchVector")
                    .Rank(EF.Functions.ToTsQuery("public.vietnamese", terms))
            }).ToListAsync(cancellationToken);

        return new PagedResult<RecipeSummaryDto>
        {
            Items = items, TotalCount = count, Page = request.Page, PageSize = request.PageSize,
            Message = count == 0 ? "Không tìm thấy công thức. Hãy thử từ khóa khác hoặc ngắn hơn." : null
        };
    }
}
