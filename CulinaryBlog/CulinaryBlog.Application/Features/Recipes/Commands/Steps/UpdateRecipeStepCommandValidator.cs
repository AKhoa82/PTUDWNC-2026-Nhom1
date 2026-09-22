using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Steps;

public class UpdateRecipeStepCommandValidator : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RuleFor(p => p.Description)
            .NotEmpty().WithMessage("Description không được để trống.")
            .MaximumLength(2000).WithMessage("Description không được vượt quá 2000 ký tự.");
            
        RuleFor(p => p.DurationMinutes)
            .GreaterThan(0).When(p => p.DurationMinutes.HasValue).WithMessage("DurationMinutes phải lớn hơn 0.");
    }
}
