using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;

public class UpdateIngredientCommandValidator : AbstractValidator<UpdateIngredientCommand>
{
    public UpdateIngredientCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nguyên liệu không được để trống.")
            .MaximumLength(100).WithMessage("Tên nguyên liệu không vượt quá 100 ký tự.");
        
        RuleFor(x => x.Notes)
            .MaximumLength(200).WithMessage("Ghi chú không vượt quá 200 ký tự.");
            
        RuleFor(x => x.Unit)
            .MaximumLength(50).WithMessage("Đơn vị không vượt quá 50 ký tự.");
    }
}
