using CulinaryBlog.Domain.Entities;
using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class DeleteRecipeStepCommandHandler : IRequestHandler<DeleteRecipeStepCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteRecipeStepCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteRecipeStepCommand request, CancellationToken cancellationToken)
    {
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
            throw new ForbiddenException("Bạn không có quyền xoá bước thực hiện của công thức này.");
        }

        var stepToRemove = recipe.Steps.FirstOrDefault(s => s.Id == request.StepId);
        if (stepToRemove == null)
        {
            throw new NotFoundException("Bước thực hiện không tồn tại.");
        }

        int deletedStepNumber = stepToRemove.StepNumber;
        
        recipe.Steps.Remove(stepToRemove);

        // Renumber remaining steps
        var remainingSteps = recipe.Steps
            .Where(s => s.StepNumber > deletedStepNumber)
            .OrderBy(s => s.StepNumber)
            .ToList();

        if (remainingSteps.Count == 0)
        {
            _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var maxStepNumber = recipe.Steps.Max(s => s.StepNumber);
        var offset = maxStepNumber + 1;

        if (_context is DbContext dbContext)
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Phase 1: Shift to a safe high range to avoid UNIQUE constraint conflicts
                foreach (var step in remainingSteps)
                {
                    step.StepNumber += offset;
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                // Phase 2: Shift back to correct numbers
                foreach (var step in remainingSteps)
                {
                    step.StepNumber -= (offset + 1);
                }
                _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
                await dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            // Fallback for non-EF environments (e.g. mock testing)
            foreach (var step in remainingSteps) step.StepNumber += offset;
            await _context.SaveChangesAsync(cancellationToken);
            
            foreach (var step in remainingSteps) step.StepNumber -= (offset + 1);
            _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}


