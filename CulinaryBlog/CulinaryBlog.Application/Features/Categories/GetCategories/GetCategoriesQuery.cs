using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.GetCategories;

public record GetCategoriesQuery() : IRequest<List<CategoryDto>>;