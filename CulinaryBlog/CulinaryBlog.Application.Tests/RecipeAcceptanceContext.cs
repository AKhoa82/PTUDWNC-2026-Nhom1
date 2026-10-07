using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Tests;

internal sealed class RecipeAcceptanceContext : DbContext, IApplicationDbContext
{
    private readonly string databaseName = Guid.NewGuid().ToString();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations => Set<RecipeCacheInvalidation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>("acceptance-v1");

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseInMemoryDatabase(databaseName);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Recipe>().OwnsOne(recipe => recipe.Nutrition);
        model.Entity<Recipe>().HasQueryFilter(recipe => !recipe.IsDeleted);
    }

    public Recipe SeedRecipe(RecipeStatus status = RecipeStatus.Published)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Món chính", Slug = "mon-chinh" };
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), Title = "Phở bò", Slug = "pho-bo",
            Category = category, CategoryId = category.Id,
            AuthorId = Guid.NewGuid(), Status = status,
            PrepTimeMinutes = 10, CookingTimeMinutes = 30, Servings = 2,
            Instructions = "Nấu nước dùng"
        };
        recipe.Steps.Add(RecipeStep.Create(recipe.Id, 1, "Nấu nước dùng"));
        recipe.Ingredients.Add(RecipeIngredient.Create(recipe.Id, "Thịt bò", 200, "g", null, 0));
        Recipes.Add(recipe);
        SaveChanges();
        return recipe;
    }
}
