using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

public class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PaginatedResult<RecipeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRecipesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<RecipeDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var normalizedRequest = NormalizeRequest(request);
        var query = BuildBaseQuery();
        query = ApplySearchFilter(query, normalizedRequest.SearchTerm);
        query = ApplyCategoryFilter(query, normalizedRequest.CategoryId);
        query = ApplySorting(query, normalizedRequest.SortBy, normalizedRequest.IsDescending);

        var totalCount = await GetTotalCountAsync(query, cancellationToken);
        var items = await GetPageItemsAsync(query, normalizedRequest.PageNumber, normalizedRequest.PageSize, cancellationToken);

        return new PaginatedResult<RecipeDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = normalizedRequest.PageNumber,
            PageSize = normalizedRequest.PageSize
        };
    }

    private static void ValidateRequest(GetRecipesQuery request)
    {
        if (request.PageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), "pageNumber phải lớn hơn 0.");
        }

        if (request.PageSize <= 0 || request.PageSize > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), "pageSize phải nằm trong khoảng từ 1 đến 50.");
        }

        var validSortFields = new[] { "title", "difficulty", "createdat" };
        var normalizedSortBy = request.SortBy?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSortBy) && !validSortFields.Contains(normalizedSortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("sortBy phải là một trong: title, difficulty, createdAt.", nameof(request.SortBy));
        }
    }

    private static GetRecipesQuery NormalizeRequest(GetRecipesQuery request)
    {
        var normalizedSearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? null
            : request.SearchTerm.Trim();

        var normalizedSortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "createdAt"
            : request.SortBy.Trim();

        return request with
        {
            SearchTerm = normalizedSearchTerm,
            SortBy = normalizedSortBy,
            IsDescending = request.IsDescending
        };
    }

    private IQueryable<Recipe> BuildBaseQuery()
    {
        return _context.Recipes
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .AsQueryable();
    }

    private static IQueryable<Recipe> ApplySearchFilter(IQueryable<Recipe> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return query;
        }

        var normalizedSearchTerm = searchTerm.Trim();

        return query.Where(r =>
            r.Title.ToLower().Contains(normalizedSearchTerm.ToLower()) ||
            r.Description.ToLower().Contains(normalizedSearchTerm.ToLower()));
    }

    private static IQueryable<Recipe> ApplyCategoryFilter(IQueryable<Recipe> query, Guid? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return query;
        }

        return query.Where(r => r.CategoryId == categoryId.Value);
    }

    private static IQueryable<Recipe> ApplySorting(IQueryable<Recipe> query, string? sortBy, bool isDescending)
    {
        return sortBy?.ToLower() switch
        {
            "title" => isDescending ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
            "difficulty" => isDescending ? query.OrderByDescending(r => r.Difficulty) : query.OrderBy(r => r.Difficulty),
            _ => isDescending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
        };
    }

    private static async Task<int> GetTotalCountAsync(IQueryable<Recipe> query, CancellationToken cancellationToken)
    {
        return await query.CountAsync(cancellationToken);
    }

    private static async Task<List<RecipeDto>> GetPageItemsAsync(
        IQueryable<Recipe> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ProjectToType<RecipeDto>()
            .ToListAsync(cancellationToken);
    }
}