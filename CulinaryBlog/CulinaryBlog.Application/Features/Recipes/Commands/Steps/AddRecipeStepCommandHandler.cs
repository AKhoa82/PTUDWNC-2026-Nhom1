using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
            throw new UnauthorizedAccessException("Bạn không có quyền thêm bước thực hiện cho công thức này.");
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
        await _context.SaveChangesAsync(cancellationToken);

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
