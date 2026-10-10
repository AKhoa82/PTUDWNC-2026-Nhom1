using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IApplicationDbContext
{
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations { get; }
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeImage> RecipeImages { get; }
    DbSet<RecipeIngredient> RecipeIngredients { get; }
    DbSet<RecipeStep> RecipeSteps { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default);
}