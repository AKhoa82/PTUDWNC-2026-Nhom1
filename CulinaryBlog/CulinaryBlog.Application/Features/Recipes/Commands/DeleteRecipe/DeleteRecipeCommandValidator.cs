using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

public sealed class DeleteRecipeCommandValidator : AbstractValidator<DeleteRecipeCommand>
{
    public DeleteRecipeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("ID công thức không được để trống.")
            .NotEqual(Guid.Empty)
            .WithMessage("ID công thức không hợp lệ.");
    }
}