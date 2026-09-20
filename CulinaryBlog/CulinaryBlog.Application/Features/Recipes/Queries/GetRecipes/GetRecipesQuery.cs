using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

public record GetRecipesQuery(
    string? SearchTerm,
    Guid? CategoryId,
    string? SortBy,
    bool IsDescending = true,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedResult<RecipeDto>>;