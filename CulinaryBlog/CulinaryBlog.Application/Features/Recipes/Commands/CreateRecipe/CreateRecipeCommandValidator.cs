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

        // Ràng buộc FR-RCP-009: Bắt buộc phải có ít nhất 1 nguyên liệu
        RuleFor(x => x.Request.Ingredients)
            .NotEmpty().WithMessage("Danh sách nguyên liệu không được để trống.")
            .Must(ingredients => ingredients != null && ingredients.Count > 0)
            .WithMessage("Công thức phải có ít nhất 1 nguyên liệu.");

        // Ràng buộc FR-RCP-010: Bắt buộc phải có ít nhất 1 bước thực hiện
        RuleFor(x => x.Request.Steps)
            .NotEmpty().WithMessage("Danh sách các bước thực hiện không được để trống.")
            .Must(steps => steps != null && steps.Count > 0)
            .WithMessage("Công thức phải có ít nhất 1 bước thực hiện.");
    }
}