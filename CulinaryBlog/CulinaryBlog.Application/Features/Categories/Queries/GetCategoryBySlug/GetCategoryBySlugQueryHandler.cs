using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

public class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCategoryBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDetailDto?> Handle(GetCategoryBySlugQuery request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Recipes)
            .FirstOrDefaultAsync(c => c.Slug == request.Slug, cancellationToken);

        if (category is null)
        {
            return null;
        }

        return new CategoryDetailDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            Recipes = category.Recipes.Select(r => new RecipeSummaryDto
            {
                Id = r.Id,
                Title = r.Title,
                Slug = r.Slug,
                Description = r.Description,
                ImageUrl = r.ImageUrl,
                CookingTimeMinutes = r.CookingTimeMinutes,
                Difficulty = r.Difficulty,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}