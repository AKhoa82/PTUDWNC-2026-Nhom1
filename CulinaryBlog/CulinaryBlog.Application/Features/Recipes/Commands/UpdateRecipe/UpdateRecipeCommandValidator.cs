using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

public class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MinimumLength(5).WithMessage("Tiêu đề phải từ 5 ký tự trở lên.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.Request.CategoryId)
            .NotEmpty().WithMessage("Danh mục không được để trống.");

        RuleFor(x => x.Request.PrepTimeMinutes)
            .GreaterThan(0).WithMessage("Thời gian chuẩn bị phải lớn hơn 0.");

        RuleFor(x => x.Request.CookingTimeMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian nấu phải là số dương hoặc bằng 0.");

        RuleFor(x => x.Request.Servings)
            .GreaterThan(0).WithMessage("Số khẩu phần phải lớn hơn 0.");

        RuleFor(x => x.Request.RowVersion)
            .NotEmpty().WithMessage("Bắt buộc phải có RowVersion để kiểm tra xung đột dữ liệu.")
            .Must(value => uint.TryParse(value, out _)).WithMessage("RowVersion không hợp lệ.");
    }
}