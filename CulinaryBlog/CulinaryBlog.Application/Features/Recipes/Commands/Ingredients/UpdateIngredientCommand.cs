using MediatR;
using CulinaryBlog.Application.DTOs;
using System;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public record UpdateIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string Name, 
    decimal? Quantity, 
    string? Unit, 
    string? Notes, 
    int SortOrder,
    string CurrentUserId,
    bool IsAdmin
) : IRequest<IngredientDto>;
