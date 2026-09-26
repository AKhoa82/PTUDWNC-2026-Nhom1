// File: CulinaryBlog.Application/Categories/Commands/CreateCategory/CreateCategoryCommandValidator.cs
using FluentValidation;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");
            
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.");
    }
}