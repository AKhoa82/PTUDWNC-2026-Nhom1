using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.GetRecipes;

public record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MaxCookTime = null,
    string Sort = "-createdAt",
    string? CurrentUserId = null,
    bool IsAdmin = false
) : IRequest<PagedResult<RecipeSummaryDto>>;
