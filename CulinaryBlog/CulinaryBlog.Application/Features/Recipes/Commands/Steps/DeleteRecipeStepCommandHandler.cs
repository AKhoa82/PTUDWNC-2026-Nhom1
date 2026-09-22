using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

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
            throw new InvalidOperationException("Recipe không tồn tại.");
        }

        var isOwner = recipe.AuthorId == request.AuthorId;

        if (!isOwner && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xoá bước thực hiện của công thức này.");
        }

        var stepToRemove = recipe.Steps.FirstOrDefault(s => s.Id == request.StepId);
        if (stepToRemove == null)
        {
            throw new InvalidOperationException("Bước thực hiện không tồn tại.");
        }

        int deletedStepNumber = stepToRemove.StepNumber;
        
        recipe.Steps.Remove(stepToRemove);

        // Renumber remaining steps
        var remainingSteps = recipe.Steps
            .Where(s => s.StepNumber > deletedStepNumber)
            .OrderBy(s => s.StepNumber)
            .ToList();

        foreach (var step in remainingSteps)
        {
            step.StepNumber -= 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
