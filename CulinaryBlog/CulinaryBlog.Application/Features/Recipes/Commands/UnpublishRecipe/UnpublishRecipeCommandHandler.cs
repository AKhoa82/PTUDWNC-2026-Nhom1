using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;

public class UnpublishRecipeCommandHandler : IRequestHandler<UnpublishRecipeCommand, RecipeDto>
{
    private readonly IApplicationDbContext _context;

    public UnpublishRecipeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDto> Handle(UnpublishRecipeCommand request, CancellationToken ct)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Category)
                .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");

        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền hủy xuất bản công thức này.");
        }

        if (recipe.Status != RecipeStatus.Published)
        {
            var unchanged = recipe.Adapt<RecipeDto>();
            unchanged.RowVersion = recipe.RowVersion.ToString();
            return unchanged;
        }

        recipe.Unpublish();

        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(ct);

        var result = recipe.Adapt<RecipeDto>();
        result.RowVersion = recipe.RowVersion.ToString();
        return result;
    }
}
