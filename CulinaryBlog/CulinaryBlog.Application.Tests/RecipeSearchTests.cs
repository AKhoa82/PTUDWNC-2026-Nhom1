using CulinaryBlog.Application.Features.Recipes.SearchRecipes;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeSearchTests
{
    [Theory]
    [InlineData("pho bo", "pho:* & bo:*")]
    [InlineData("  PHỞ   bò  ", "phở:* & bò:*")]
    [InlineData("pho | !bo:* <-> 'ga'", "pho:* & bo:* & ga:*")]
    [InlineData("pho'); DROP TABLE Recipes;--", "pho:* & drop:* & table:* & recipes:*")]
    [InlineData("pho PHO", "pho:*")]
    [InlineData("!!!", "")]
    [InlineData("pho\u031b\u0309", "phở:*")]
    public void Prefix_query_uses_only_normalized_words(string input, string expected)
        => Assert.Equal(expected, RecipeSearchTerms.ToPrefixQuery(input));

    [Theory]
    [InlineData(null, 1, 12, false)]
    [InlineData(" ", 1, 12, false)]
    [InlineData("a", 1, 12, false)]
    [InlineData("!!!", 1, 12, false)]
    [InlineData("phở", 0, 12, false)]
    [InlineData("phở", 1, 51, false)]
    [InlineData("phở", 1, 0, false)]
    [InlineData("phở", 1, 12, true)]
    [InlineData("pho bo", int.MaxValue, 50, true)]
    public void Validates_search_and_pagination(string? term, int page, int size, bool valid)
        => Assert.Equal(valid, new SearchRecipesQueryValidator().Validate(new SearchRecipesQuery(term, page, size)).IsValid);

    [Theory]
    [InlineData("Easy", true)]
    [InlineData("medium", true)]
    [InlineData("HARD", true)]
    [InlineData("Expert", true)]
    [InlineData("invalid", false)]
    [InlineData("", true)]
    public void Validates_difficulty(string? difficulty, bool valid)
    {
        var query = new SearchRecipesQuery("pho", Difficulty: difficulty);
        Assert.Equal(valid, new SearchRecipesQueryValidator().Validate(query).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(60, true)]
    [InlineData(-1, false)]
    public void Validates_maxCookTime(int? maxCookTime, bool valid)
    {
        var query = new SearchRecipesQuery("pho", MaxCookTime: maxCookTime);
        Assert.Equal(valid, new SearchRecipesQueryValidator().Validate(query).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Validates_minServings(int? minServings, bool valid)
    {
        var query = new SearchRecipesQuery("pho", MinServings: minServings);
        Assert.Equal(valid, new SearchRecipesQueryValidator().Validate(query).IsValid);
    }
}
