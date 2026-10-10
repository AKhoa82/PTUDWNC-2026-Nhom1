using MediatR;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;

public record PublishRecipeCommand(Guid Id, string CurrentUserId, bool IsAdmin) : IRequest<RecipeDto>;