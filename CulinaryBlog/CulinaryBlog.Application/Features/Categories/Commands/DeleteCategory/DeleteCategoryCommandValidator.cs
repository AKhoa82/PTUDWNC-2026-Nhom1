using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID danh mục không được để trống.")
            .NotEqual(Guid.Empty).WithMessage("ID danh mục không hợp lệ.");
    }
}