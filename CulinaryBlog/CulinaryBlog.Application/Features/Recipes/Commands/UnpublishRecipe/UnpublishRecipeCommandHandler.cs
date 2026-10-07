using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;

public class UnpublishRecipeCommandHandler : IRequestHandler<UnpublishRecipeCommand, RecipeDetailDto>
{
    private readonly IApplicationDbContext _context;

    public UnpublishRecipeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDetailDto> Handle(UnpublishRecipeCommand request, CancellationToken ct)
    {
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");

        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền hủy xuất bản công thức này.");
        }

        recipe.Unpublish();

        _context.Recipes.Update(recipe);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(ct);

        var result = recipe.Adapt<RecipeDetailDto>();
        result.RowVersion = recipe.RowVersion.ToString();
        return result;
    }
}
