using CulinaryBlog.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes;
using CulinaryBlog.Domain.Exceptions;

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
        await using var transaction = await RecipeMutationTransaction.BeginAsync(
            _context, request.RecipeId, cancellationToken);

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

        if (recipe.Status == RecipeStatus.Published &&
            await _context.RecipeIngredients.CountAsync(ri => ri.RecipeId == request.RecipeId, cancellationToken) <= 1)
            throw new DomainException("Recipe đã xuất bản phải có ít nhất 1 nguyên liệu.");

        _context.RecipeIngredients.Remove(ingredient);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }
}


