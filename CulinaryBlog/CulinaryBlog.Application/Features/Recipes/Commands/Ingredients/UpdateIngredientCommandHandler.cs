using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public class UpdateIngredientCommandHandler : IRequestHandler<UpdateIngredientCommand, IngredientDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateIngredientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IngredientDto> Handle(UpdateIngredientCommand request, CancellationToken cancellationToken)
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

        ingredient.Update(
            request.Name,
            request.Quantity,
            request.Unit,
            request.Notes,
            request.SortOrder
        );

        await _context.SaveChangesAsync(cancellationToken);

        return new IngredientDto
        {
            Id = ingredient.Id,
            Name = ingredient.Name,
            Quantity = ingredient.Quantity,
            Unit = ingredient.Unit,
            Notes = ingredient.Notes,
            SortOrder = ingredient.SortOrder
        };
    }
}
