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
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => EF.Property<NpgsqlTsVector>(r, "SearchVector")
                .Rank(EF.Functions.ToTsQuery("public.vietnamese", terms)))
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
