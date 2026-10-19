using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;

public class ArchiveRecipeCommandValidator : AbstractValidator<ArchiveRecipeCommand>
{
    public ArchiveRecipeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID công thức không được để trống.")
            .NotEqual(Guid.Empty).WithMessage("ID công thức không hợp lệ.");
    }
}