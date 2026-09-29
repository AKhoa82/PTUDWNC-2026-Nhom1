using MediatR;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;

public record UnpublishRecipeCommand(Guid Id, string CurrentUserId, bool IsAdmin) : IRequest<RecipeDetailDto>;