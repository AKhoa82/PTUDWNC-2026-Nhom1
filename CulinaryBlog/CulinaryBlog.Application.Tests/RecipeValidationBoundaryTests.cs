using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;
using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeValidationBoundaryTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(999, false)]
    public void Update_recipe_accepts_only_defined_difficulties(int difficulty, bool expected)
    {
        var request = new UpdateRecipeRequest
        {
            Title = "Phở bò Hà Nội",
            CategoryId = Guid.NewGuid(),
            PrepTimeMinutes = 10,
            CookingTimeMinutes = 0,
            Servings = 2,
            Difficulty = (RecipeDifficulty)difficulty,
            Instructions = "Nấu nước dùng",
            RowVersion = "1"
        };

        var result = new UpdateRecipeCommandValidator().Validate(
            new UpdateRecipeCommand(Guid.NewGuid(), request, Guid.NewGuid().ToString(), false));

        Assert.Equal(expected, result.IsValid);
        if (!expected)
            Assert.Contains(result.Errors, error => error.PropertyName == "Request.Difficulty");
    }

    [Fact]
    public void Add_and_update_ingredient_allow_null_quantity_but_require_positive_value()
    {
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var actorId = Guid.NewGuid().ToString();
        var addValidator = new AddIngredientCommandValidator();
        var updateValidator = new UpdateIngredientCommandValidator();

        foreach (var (quantity, expected) in new (decimal? Quantity, bool Valid)[]
                 { (null, true), (-1m, false), (0m, false), (0.001m, true) })
        {
            var add = new AddIngredientCommand(recipeId, "Muối", quantity, null, null, 0, actorId, false);
            var update = new UpdateIngredientCommand(recipeId, ingredientId, "Muối", quantity, null, null, 0, actorId, false);

            var addResult = addValidator.Validate(add);
            var updateResult = updateValidator.Validate(update);
            Assert.Equal(expected, addResult.IsValid);
            Assert.Equal(expected, updateResult.IsValid);
            if (!expected)
            {
                Assert.Contains(addResult.Errors, error => error.PropertyName == "Quantity");
                Assert.Contains(updateResult.Errors, error => error.PropertyName == "Quantity");
            }
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Create_recipe_validates_nested_step_description_length(int length, bool expected)
    {
        var request = new CreateRecipeRequest
        {
            Title = "Phở bò Hà Nội",
            CategoryId = Guid.NewGuid(),
            PrepTimeMinutes = 10,
            CookingTimeMinutes = 0,
            Servings = 2,
            Instructions = "Nấu nước dùng",
            Steps = new List<CreateStepRequestDto> { new() { Description = new string('a', length) } }
        };

        var result = new CreateRecipeCommandValidator().Validate(
            new CreateRecipeCommand(request, Guid.NewGuid().ToString()));

        Assert.Equal(expected, result.IsValid);
        if (!expected)
            Assert.Contains(result.Errors, error => error.PropertyName == "Request.Steps[0].Description");
    }
}
