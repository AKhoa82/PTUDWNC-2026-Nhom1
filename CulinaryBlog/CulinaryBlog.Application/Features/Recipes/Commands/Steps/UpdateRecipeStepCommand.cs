using MediatR;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class UpdateRecipeStepCommand : IRequest<RecipeStepDto>
{
    public Guid RecipeId { get; set; }
    public Guid StepId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }
    
    public string? AuthorId { get; set; }
    public bool IsAdmin { get; set; }
}
