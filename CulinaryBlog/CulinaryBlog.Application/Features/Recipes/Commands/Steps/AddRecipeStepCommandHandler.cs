using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class AddRecipeStepCommandHandler : IRequestHandler<AddRecipeStepCommand, RecipeStepDto>
{
    private readonly IApplicationDbContext _context;

    public AddRecipeStepCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeStepDto> Handle(AddRecipeStepCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await RecipeMutationTransaction.BeginAsync(
            _context, request.RecipeId, cancellationToken);

        var recipe = await _context.Recipes
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

        if (recipe == null)
        {
            throw new NotFoundException("Recipe không tồn tại.");
        }

        var isOwner = Guid.TryParse(request.AuthorId, out var parsedUserId) && recipe.AuthorId == parsedUserId;

        if (!isOwner && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền thêm bước thực hiện cho công thức này.");
        }

        int nextStepNumber = 1;
        if (recipe.Steps.Any())
        {
            nextStepNumber = recipe.Steps.Max(s => s.StepNumber) + 1;
        }

        var step = RecipeStep.Create(
            request.RecipeId,
            nextStepNumber,
            request.Description,
            title: request.Title,
            durationMinutes: request.DurationMinutes,
            imageUrl: request.ImageUrl
        );

        recipe.Steps.Add(step);
        _context.RecipeSteps.Add(step);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return new RecipeStepDto
        {
            Id = step.Id,
            StepNumber = step.StepNumber,
            Title = step.Title,
            Description = step.Description,
            DurationMinutes = step.DurationMinutes,
            ImageUrl = step.ImageUrl
        };
    }
}

