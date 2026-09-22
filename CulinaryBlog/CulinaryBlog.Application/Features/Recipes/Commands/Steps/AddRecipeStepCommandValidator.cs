using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class AddRecipeStepCommandValidator : AbstractValidator<AddRecipeStepCommand>
{
    public AddRecipeStepCommandValidator()
    {
        RuleFor(p => p.Title)
            .MaximumLength(200).WithMessage("Title không được vượt quá 200 ký tự.")
            .When(p => !string.IsNullOrWhiteSpace(p.Title));

        RuleFor(p => p.Description)
            .NotEmpty().WithMessage("Description không được để trống.")
            .MaximumLength(2000).WithMessage("Description không được vượt quá 2000 ký tự.");
            
        RuleFor(p => p.DurationMinutes)
            .GreaterThan(0).When(p => p.DurationMinutes.HasValue).WithMessage("DurationMinutes phải lớn hơn 0.");
    }
}
