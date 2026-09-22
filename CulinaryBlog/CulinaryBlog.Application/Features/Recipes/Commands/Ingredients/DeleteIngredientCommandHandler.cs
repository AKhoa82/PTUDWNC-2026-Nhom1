using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            throw new InvalidOperationException("Recipe không tồn tại."); // Should be a custom NotFoundException
        }

        if (recipe.AuthorId != request.CurrentUserId && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền sửa công thức này.");
        }

        var ingredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(ri => ri.Id == request.IngredientId && ri.RecipeId == request.RecipeId, cancellationToken);

        if (ingredient == null)
        {
            throw new InvalidOperationException("Nguyên liệu không tồn tại."); // Custom exception
        }

        _context.RecipeIngredients.Remove(ingredient);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
