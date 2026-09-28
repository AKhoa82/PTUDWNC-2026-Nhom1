using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

public record UpdateRecipeCommand(
    Guid Id, 
    UpdateRecipeRequest Request, 
    string CurrentUserId, 
    bool IsAdmin
) : IRequest<RecipeDetailDto>;