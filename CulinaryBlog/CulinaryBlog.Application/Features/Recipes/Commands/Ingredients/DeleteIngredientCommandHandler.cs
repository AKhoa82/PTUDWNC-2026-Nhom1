using CulinaryBlog.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public class DeleteIngredientCommandHandler : IRequestHandler<DeleteIngredientCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteIngredientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

        if (recipe == null)
        {
            throw new NotFoundException("Recipe không tồn tại."); // Should be a custom NotFoundException
        }

        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền sửa công thức này.");
        }

        var ingredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(ri => ri.Id == request.IngredientId && ri.RecipeId == request.RecipeId, cancellationToken);

        if (ingredient == null)
        {
            throw new NotFoundException("Nguyên liệu không tồn tại."); // Custom exception
        }

        _context.RecipeIngredients.Remove(ingredient);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
        await _context.SaveChangesAsync(cancellationToken);
    }
}

