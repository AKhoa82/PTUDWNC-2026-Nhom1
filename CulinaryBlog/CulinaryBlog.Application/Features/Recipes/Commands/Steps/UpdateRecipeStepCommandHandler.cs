using CulinaryBlog.Domain.Entities;
using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Common.Exceptions;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class UpdateRecipeStepCommandHandler : IRequestHandler<UpdateRecipeStepCommand, RecipeStepDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateRecipeStepCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeStepDto> Handle(UpdateRecipeStepCommand request, CancellationToken cancellationToken)
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
            throw new ForbiddenException("Bạn không có quyền cập nhật bước thực hiện cho công thức này.");
        }

        var step = recipe.Steps.FirstOrDefault(s => s.Id == request.StepId);
        if (step == null)
        {
            throw new NotFoundException("Bước thực hiện không tồn tại.");
        }

        step.Update(
            request.Description,
            title: request.Title,
            durationMinutes: request.DurationMinutes,
            imageUrl: request.ImageUrl
        );

        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
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


