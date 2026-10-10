using FluentValidation;
using CulinaryBlog.Application.DTOs;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MinimumLength(5).WithMessage("Tiêu đề phải có ít nhất 5 ký tự.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.Request.ImageUrl)
            .MaximumLength(500).WithMessage("ImageUrl không được vượt quá 500 ký tự.");

        RuleFor(x => x.Request.CategoryId)
            .NotEmpty().WithMessage("Danh mục không được để trống.");

        RuleFor(x => x.Request.PrepTimeMinutes)
            .GreaterThan(0).WithMessage("Thời gian chuẩn bị phải lớn hơn 0.");

        RuleFor(x => x.Request.CookingTimeMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian nấu không được âm.");

        RuleFor(x => x.Request.Servings)
            .GreaterThan(0).WithMessage("Số khẩu phần ăn phải lớn hơn 0.");

        RuleFor(x => x.Request.Difficulty)
            .IsInEnum().WithMessage("Độ khó không hợp lệ.");

        RuleFor(x => x.Request.Instructions)
            .NotEmpty().WithMessage("Hướng dẫn không được để trống.");

        RuleFor(x => x.Request.Ingredients)
            .NotNull().WithMessage("Danh sách nguyên liệu không được để null.");
            
        RuleForEach(x => x.Request.Ingredients).SetValidator(new CreateIngredientRequestDtoValidator());

        RuleFor(x => x.Request.Steps)
            .NotNull().WithMessage("Danh sách các bước thực hiện không được để null.");

        RuleForEach(x => x.Request.Steps).SetValidator(new CreateStepRequestDtoValidator());
    }
}

public class CreateIngredientRequestDtoValidator : AbstractValidator<CreateIngredientRequestDto>
{
    public CreateIngredientRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nguyên liệu không được để trống.")
            .MaximumLength(100).WithMessage("Tên nguyên liệu không được vượt quá 100 ký tự.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).When(x => x.Quantity.HasValue)
            .WithMessage("Số lượng phải lớn hơn 0.");
    }
}

public class CreateStepRequestDtoValidator : AbstractValidator<CreateStepRequestDto>
{
    public CreateStepRequestDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả bước thực hiện không được để trống.")
            .MaximumLength(2000).WithMessage("Mô tả bước thực hiện không được vượt quá 2000 ký tự.");
    }
}
