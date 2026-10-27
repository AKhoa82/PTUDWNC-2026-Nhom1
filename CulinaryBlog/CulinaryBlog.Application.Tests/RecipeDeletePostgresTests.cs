using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeDeletePostgresTests
{
    [PostgresFact]
    public async Task Delete_soft_hides_recipe_but_retains_related_data_and_rejects_non_owner()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_delete_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = database,
                Pooling = false
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var context = new ApplicationDbContext(options);
            await context.Database.MigrateAsync();

            var author = new User { Id = Guid.NewGuid(), UserName = "delete-author", FullName = "Delete Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = "Đậu hũ chiên", Slug = "dau-hu-chien-delete",
                Description = "Món đậu hũ chiên", CategoryId = category.Id, AuthorId = author.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10, Servings = 2,
                Status = RecipeStatus.Published, PublishedAt = DateTime.UtcNow
            };
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, 1, "Chiên đậu hũ"));
            recipe.Ingredients.Add(RecipeIngredient.Create(recipe.Id, "Đậu hũ", 2, "miếng", null, 0));
            recipe.Images.Add(new RecipeImage
            {
                RecipeId = recipe.Id, OriginalUrl = "https://example.test/dau-hu.jpg", IsPrimary = true
            });
            context.Users.Add(author);
            context.Categories.Add(category);
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();

            var handler = new DeleteRecipeCommandHandler(context, NullLogger<DeleteRecipeCommandHandler>.Instance);
            var listing = new GetRecipesQueryHandler(context);
            Assert.Contains((await listing.Handle(new GetRecipesQuery(), default)).Items,
                item => item.Id == recipe.Id);

            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
                new DeleteRecipeCommand(recipe.Id, Guid.NewGuid().ToString(), false), default));
            Assert.False(recipe.IsDeleted);

            await handler.Handle(new DeleteRecipeCommand(recipe.Id, author.Id.ToString(), false), default);
            context.ChangeTracker.Clear();
            Assert.False(await context.Recipes.AnyAsync(item => item.Id == recipe.Id));
            Assert.DoesNotContain((await listing.Handle(new GetRecipesQuery(), default)).Items,
                item => item.Id == recipe.Id);

            var retained = await context.Recipes.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(item => item.Id == recipe.Id);
            Assert.True(retained.IsDeleted);
            Assert.NotNull(retained.UpdatedAt);
            Assert.Equal(1, await context.RecipeSteps.CountAsync(item => item.RecipeId == recipe.Id));
            Assert.Equal(1, await context.RecipeIngredients.CountAsync(item => item.RecipeId == recipe.Id));
            Assert.Equal(1, await context.RecipeImages.CountAsync(item => item.RecipeId == recipe.Id));
            Assert.Equal(recipe.Slug,
                (await context.RecipeCacheInvalidations.SingleAsync()).RecipeSlug);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
