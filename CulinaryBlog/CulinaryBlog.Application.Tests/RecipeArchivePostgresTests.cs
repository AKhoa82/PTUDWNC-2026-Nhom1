using CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeArchivePostgresTests
{
    [PostgresFact]
    public async Task Archive_hides_public_recipe_and_unarchive_restores_draft_without_extra_writes()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_archive_test_" + Guid.NewGuid().ToString("N");
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

            var author = new User { Id = Guid.NewGuid(), UserName = "archive-author", FullName = "Archive Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = "Đậu hũ chiên", Slug = "dau-hu-chien-archive",
                Description = "Món đậu hũ chiên", CategoryId = category.Id, AuthorId = author.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10, Servings = 2,
                Status = RecipeStatus.Published, PublishedAt = DateTime.UtcNow
            };
            context.Users.Add(author);
            context.Categories.Add(category);
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();

            var handler = new ArchiveRecipeCommandHandler(context);
            var listing = new GetRecipesQueryHandler(context);
            Assert.Contains((await listing.Handle(new GetRecipesQuery(), default)).Items,
                item => item.Id == recipe.Id);

            var actorId = author.Id.ToString();
            var archivedResult = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Archived", archivedResult.Status);
            context.ChangeTracker.Clear();
            var archived = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(RecipeStatus.Archived, archived.Status);
            Assert.DoesNotContain((await listing.Handle(new GetRecipesQuery(), default)).Items,
                item => item.Id == recipe.Id);
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync());

            var repeatedArchive = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Archived", repeatedArchive.Status);
            context.ChangeTracker.Clear();
            var stillArchived = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(archived.UpdatedAt, stillArchived.UpdatedAt);
            Assert.Equal(archived.RowVersion, stillArchived.RowVersion);
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync());

            var restoredResult = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, actorId, false, true), default);
            Assert.Equal("Draft", restoredResult.Status);
            context.ChangeTracker.Clear();
            var restored = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(RecipeStatus.Draft, restored.Status);
            Assert.Equal(2, await context.RecipeCacheInvalidations.CountAsync());

            var repeatedRestore = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, actorId, false, true), default);
            Assert.Equal("Draft", repeatedRestore.Status);
            context.ChangeTracker.Clear();
            var stillDraft = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(restored.UpdatedAt, stillDraft.UpdatedAt);
            Assert.Equal(restored.RowVersion, stillDraft.RowVersion);
            Assert.Equal(2, await context.RecipeCacheInvalidations.CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
