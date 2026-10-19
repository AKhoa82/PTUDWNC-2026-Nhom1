using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

public record DeleteRecipeCommand(
    Guid Id,
    string CurrentUserId,
    bool IsAdmin
) : IRequest<Unit>;