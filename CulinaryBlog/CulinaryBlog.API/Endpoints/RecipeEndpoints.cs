using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes").WithTags("Recipes");

        group.MapGet("", async (
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? categoryId,
            [FromQuery] string? sortBy,
            [FromQuery] bool? isDescending,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (pageNumber is <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pageNumber"] = ["pageNumber phải lớn hơn 0."]
                });
            }

            if (pageSize is <= 0 or > 50)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pageSize"] = ["pageSize phải nằm trong khoảng từ 1 đến 50."]
                });
            }

            var normalizedSearchTerm = NormalizeSearchTerm(searchTerm);
            var normalizedSortBy = NormalizeSortBy(sortBy);

            if (!IsValidSortField(normalizedSortBy))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sortBy"] = ["sortBy phải là một trong: title, difficulty, createdAt."]
                });
            }

            var query = new GetRecipesQuery(
                normalizedSearchTerm,
                categoryId,
                normalizedSortBy,
                isDescending ?? true,
                pageNumber ?? 1,
                pageSize ?? 10
            );

            var result = await mediator.Send(query, cancellationToken);

            return Results.Ok(new
            {
                data = result.Items,
                meta = new
                {
                    totalCount = result.TotalCount,
                    pageNumber = result.PageNumber,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages
                }
            });
        });
    }

    private static string? NormalizeSearchTerm(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        return searchTerm.Trim();
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : sortBy.Trim();
    }

    private static bool IsValidSortField(string sortBy)
    {
        var allowedSortFields = new[] { "title", "difficulty", "createdAt" };
        return allowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase);
    }
}