// File: CulinaryBlog.Application/Categories/Commands/UpdateCategory/UpdateCategoryCommandValidator.cs
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using FluentValidation;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id danh mục không hợp lệ.");
        
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");
            
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.");
    }
}