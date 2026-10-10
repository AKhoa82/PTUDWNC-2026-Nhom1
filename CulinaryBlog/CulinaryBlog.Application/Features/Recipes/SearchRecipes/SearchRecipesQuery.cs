using System.Text;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.SearchRecipes;

public record SearchRecipesQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = null)
    : IRequest<PagedResult<RecipeSummaryDto>>;

public static partial class RecipeSearchTerms
{
    // Only letters/numbers enter tsquery syntax; operators and punctuation are separators.
    public static string ToPrefixQuery(string? value) => string.Join(" & ",
        Tokens().Matches((value ?? "").Normalize(NormalizationForm.FormC))
            .Select(match => match.Value.ToLowerInvariant()).Distinct()
            .Select(token => token + ":*"));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Tokens();
}

public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    public SearchRecipesQueryValidator()
    {
        RuleFor(q => q.SearchTerm)
            .NotNull()
            .WithMessage("Từ khóa tìm kiếm là bắt buộc.")
            .Must(term => term?.Trim().Length >= 2)
            .WithMessage("Từ khóa tìm kiếm phải có ít nhất 2 ký tự.")
            .Must(term => !string.IsNullOrEmpty(RecipeSearchTerms.ToPrefixQuery(term)))
            .WithMessage("Từ khóa phải chứa chữ hoặc số.")
            .OverridePropertyName("q");
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 50);

        RuleFor(q => q.Difficulty)
            .IsEnumName(typeof(CulinaryBlog.Domain.Entities.RecipeDifficulty), caseSensitive: false)
            .When(q => !string.IsNullOrEmpty(q.Difficulty))
            .WithMessage("Độ khó không hợp lệ. Chỉ chấp nhận Easy, Medium, Hard, Expert.");

        RuleFor(q => q.MaxCookTime)
            .GreaterThanOrEqualTo(0)
            .When(q => q.MaxCookTime.HasValue);

        RuleFor(q => q.MinServings)
            .GreaterThan(0)
            .When(q => q.MinServings.HasValue);
    }
}

public interface IRecipeSearchService
{
    Task<PagedResult<RecipeSummaryDto>> SearchAsync(SearchRecipesQuery query, CancellationToken cancellationToken);
}

public sealed class SearchRecipesQueryHandler(IRecipeSearchService search)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public Task<PagedResult<RecipeSummaryDto>> Handle(SearchRecipesQuery request, CancellationToken cancellationToken)
        => search.SearchAsync(request, cancellationToken);
}
