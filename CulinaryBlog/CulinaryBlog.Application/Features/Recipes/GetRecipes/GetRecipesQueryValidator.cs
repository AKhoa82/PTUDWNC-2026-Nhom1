using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.GetRecipes;

public sealed class GetRecipesQueryValidator : AbstractValidator<GetRecipesQuery>
{
    public GetRecipesQueryValidator()
    {
        RuleFor(query => query.MaxCookTime).GreaterThanOrEqualTo(0)
            .WithMessage("Thời gian nấu tối đa phải là số nguyên không âm.");
        RuleFor(query => query.MinServings).GreaterThanOrEqualTo(1)
            .WithMessage("Số khẩu phần tối thiểu phải là số nguyên từ 1 trở lên.");
        RuleFor(query => query.Difficulty).IsInEnum()
            .WithMessage("Độ khó phải là Easy, Medium, Hard hoặc Expert.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}
