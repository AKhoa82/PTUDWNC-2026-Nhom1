using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

public class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public GetCategoryBySlugQueryHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<CategoryDetailDto?> Handle(GetCategoryBySlugQuery request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Slug == request.Slug)
            .Select(c => new { c.Id, c.Name, c.Slug, c.Description })
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
            throw new CulinaryBlog.Application.Common.Exceptions.NotFoundException(
                $"Không tìm thấy danh mục với slug: {request.Slug}");

        var publishedCount = await _context.Recipes
            .CountAsync(r => r.CategoryId == category.Id && r.Status == RecipeStatus.Published, cancellationToken);
        var recipes = await _sender.Send(new GetRecipesQuery(
            Page: request.Page,
            PageSize: request.PageSize,
            CategoryId: category.Id,
            CurrentUserId: request.CurrentUserId,
            CategoryView: true), cancellationToken);

        return new CategoryDetailDto
        {
            Category = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                RecipeCount = publishedCount
            },
            Recipes = recipes
        };
    }
}
