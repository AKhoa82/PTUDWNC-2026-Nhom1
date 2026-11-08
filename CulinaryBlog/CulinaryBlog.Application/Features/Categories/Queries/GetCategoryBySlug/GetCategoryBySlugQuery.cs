using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

public record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12,
    string? CurrentUserId = null) : IRequest<CategoryDetailDto?>;
