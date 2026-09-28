using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;
using CulinaryBlog.Domain.Entities;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class UpdateRecipeCommandHandlerTests
{
    private sealed class TestContext : DbContext, IApplicationDbContext
    {
        public Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("test");
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<User> Users => throw new NotSupportedException();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
        public DbSet<RefreshToken> RefreshTokens => throw new NotSupportedException();
        public DbSet<StoredFile> StoredFiles => throw new NotSupportedException();
        public DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations => Set<RecipeCacheInvalidation>();
        public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
        
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Recipe>().OwnsOne(r => r.Nutrition);
        }
    }

    [Fact]
    public async Task UpdateRecipe_Success()
    {
        // Arrange
        using var db = new TestContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Category 1", Slug = "category-1" };
        var authorId = "author-1";
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Old Title",
            AuthorId = authorId,
            Category = category
        };
        recipe.RowVersion = new byte[] { 1, 2, 3 };

        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var handler = new UpdateRecipeCommandHandler(db);
        var requestDto = new UpdateRecipeRequest
        {
            Title = "New Title",
            Description = "New Description",
            CategoryId = category.Id,
            PrepTimeMinutes = 15,
            CookingTimeMinutes = 30,
            Servings = 2,
            Difficulty = RecipeDifficulty.Medium,
            Instructions = "Step 1, Step 2",
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            Nutrition = new UpdateRecipeNutritionDto
            {
                Calories = 100,
                Protein = 10,
                Carbs = 20,
                Fat = 5
            }
        };

        var command = new UpdateRecipeCommand(recipe.Id, requestDto, authorId, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Title", result.Title);
        Assert.Equal(15, result.PrepTimeMinutes);
        
        var updatedRecipe = await db.Recipes.FindAsync(recipe.Id);
        Assert.NotNull(updatedRecipe);
        Assert.Equal("New Title", updatedRecipe.Title);
        Assert.NotNull(updatedRecipe.Nutrition);
        Assert.Equal(100m, updatedRecipe.Nutrition.Calories);
    }
}
