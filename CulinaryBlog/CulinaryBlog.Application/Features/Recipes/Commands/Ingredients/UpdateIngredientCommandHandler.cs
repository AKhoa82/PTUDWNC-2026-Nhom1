using CulinaryBlog.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;

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

        ingredient.Update(
            request.Name,
            request.Quantity,
            request.Unit,
            request.Notes,
            request.SortOrder
        );

        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
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