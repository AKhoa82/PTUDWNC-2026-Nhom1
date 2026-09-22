using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class DeleteRecipeStepCommand : IRequest
{
    public Guid RecipeId { get; set; }
    public Guid StepId { get; set; }
    
    public string? AuthorId { get; set; }
    public bool IsAdmin { get; set; }
}
