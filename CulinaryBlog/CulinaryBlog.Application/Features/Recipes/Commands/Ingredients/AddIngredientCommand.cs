using MediatR;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public record AddIngredientCommand(
    Guid RecipeId, 
    string Name, 
    decimal? Quantity, 
    string? Unit, 
    string? Notes, 
    int SortOrder,
    string CurrentUserId,
    bool IsAdmin
) : IRequest<IngredientDto>;
