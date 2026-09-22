using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;

public record GetRecipeBySlugQuery(string Slug) : IRequest<RecipeDetailDto?>;