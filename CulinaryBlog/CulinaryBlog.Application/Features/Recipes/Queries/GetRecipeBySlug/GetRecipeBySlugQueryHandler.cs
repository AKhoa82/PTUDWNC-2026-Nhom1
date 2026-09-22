using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;

public class GetRecipeBySlugQueryHandler : IRequestHandler<GetRecipeBySlugQuery, RecipeDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetRecipeBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDetailDto?> Handle(GetRecipeBySlugQuery request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .AsNoTracking()
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Slug == request.Slug, cancellationToken);

        if (recipe is null)
        {
            return null;
        }

        if (recipe.Status != RecipeStatus.Published)
        {
            if (!request.IsAdmin && recipe.AuthorId != request.CurrentUserId)
            {
                throw new UnauthorizedAccessException("Không có quyền xem công thức chưa xuất bản.");
            }
        }

        return new RecipeDetailDto
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Slug = recipe.Slug,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Servings = recipe.Servings,
            Difficulty = recipe.Difficulty.ToString(),
            Instructions = recipe.Instructions,
            Status = recipe.Status.ToString(),
            CategoryId = recipe.CategoryId,
            CategoryName = recipe.Category?.Name ?? string.Empty,
            AuthorId = recipe.AuthorId,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt,
            PublishedAt = recipe.PublishedAt
        };
    }
}