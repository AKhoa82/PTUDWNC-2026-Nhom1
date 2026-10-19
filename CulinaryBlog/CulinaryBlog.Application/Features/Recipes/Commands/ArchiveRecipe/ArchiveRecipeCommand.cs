using MediatR;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;

public record ArchiveRecipeCommand(Guid Id, string CurrentUserId, bool IsAdmin) : IRequest<RecipeDto>;