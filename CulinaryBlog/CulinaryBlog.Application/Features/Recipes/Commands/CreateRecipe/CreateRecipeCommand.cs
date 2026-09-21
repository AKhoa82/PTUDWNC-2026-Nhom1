using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public record CreateRecipeCommand(CreateRecipeRequest Request, string? AuthorId) : IRequest<Guid>;