using MediatR;
using System;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public record DeleteIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string CurrentUserId,
    bool IsAdmin
) : IRequest;
