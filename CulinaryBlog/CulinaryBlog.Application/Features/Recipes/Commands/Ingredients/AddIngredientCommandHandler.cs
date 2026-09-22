using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public class AddIngredientCommandHandler : IRequestHandler<AddIngredientCommand, IngredientDto>
{
    private readonly IApplicationDbContext _context;

    public AddIngredientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IngredientDto> Handle(AddIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

        if (recipe == null)
        {
            throw new InvalidOperationException("Recipe không tồn tại."); // Should be a custom Exception like NotFoundException
        }

        if (recipe.AuthorId != request.CurrentUserId && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền sửa công thức này.");
        }

        var ingredient = RecipeIngredient.Create(
            request.RecipeId, 
            request.Name, 
            request.Quantity, 
            request.Unit, 
            request.Notes, 
            request.SortOrder
        );

        _context.RecipeIngredients.Add(ingredient);
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
