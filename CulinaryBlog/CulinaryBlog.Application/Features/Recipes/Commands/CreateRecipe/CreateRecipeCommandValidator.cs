using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.Request.CategoryId)
            .NotEmpty().WithMessage("Danh mục không được để trống.");

        RuleFor(x => x.Request.Ingredients)
            .NotNull().WithMessage("Danh sách nguyên liệu không được để null.");

        RuleFor(x => x.Request.Steps)
            .NotNull().WithMessage("Danh sách các bước thực hiện không được để null.");
    }
}