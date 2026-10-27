using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipePublishPostgresTests
{
    [PostgresFact]
    public async Task Publish_and_unpublish_are_idempotent_and_archived_recipe_cannot_publish()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_publish_test_" + Guid.NewGuid().ToString("N");
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

            var author = new User { Id = Guid.NewGuid(), UserName = "publish-author", FullName = "Publish Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = "Đậu hũ chiên", Slug = "dau-hu-chien",
                Description = "Món đậu hũ chiên", CategoryId = category.Id, AuthorId = author.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10, Servings = 2,
                Status = RecipeStatus.Draft
            };
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, 1, "Chiên đậu hũ"));
            recipe.Ingredients.Add(RecipeIngredient.Create(recipe.Id, "Đậu hũ", 2, "miếng", null, 0));
            context.Users.Add(author);
            context.Categories.Add(category);
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();

            var publish = new PublishRecipeCommandHandler(context);
            var unpublish = new UnpublishRecipeCommandHandler(context);
            var actorId = author.Id.ToString();
            var firstPublish = await publish.Handle(new PublishRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Published", firstPublish.Status);

            context.ChangeTracker.Clear();
            var published = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.NotNull(published.UpdatedAt);
            Assert.NotNull(published.PublishedAt);
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync());

            var repeatedPublish = await publish.Handle(new PublishRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Published", repeatedPublish.Status);
            context.ChangeTracker.Clear();
            var stillPublished = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(published.UpdatedAt, stillPublished.UpdatedAt);
            Assert.Equal(published.RowVersion, stillPublished.RowVersion);
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync());

            var firstUnpublish = await unpublish.Handle(new UnpublishRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Draft", firstUnpublish.Status);
            context.ChangeTracker.Clear();
            var draft = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(published.PublishedAt, draft.PublishedAt);
            Assert.Equal(2, await context.RecipeCacheInvalidations.CountAsync());

            var repeatedUnpublish = await unpublish.Handle(new UnpublishRecipeCommand(recipe.Id, actorId, false), default);
            Assert.Equal("Draft", repeatedUnpublish.Status);
            context.ChangeTracker.Clear();
            var stillDraft = await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id);
            Assert.Equal(draft.UpdatedAt, stillDraft.UpdatedAt);
            Assert.Equal(draft.RowVersion, stillDraft.RowVersion);
            Assert.Equal(2, await context.RecipeCacheInvalidations.CountAsync());

            var archived = await context.Recipes.SingleAsync(item => item.Id == recipe.Id);
            archived.Archive();
            await context.SaveChangesAsync();
            await Assert.ThrowsAsync<DomainException>(() =>
                publish.Handle(new PublishRecipeCommand(recipe.Id, actorId, false), default));
            context.ChangeTracker.Clear();
            Assert.Equal(RecipeStatus.Archived,
                (await context.Recipes.AsNoTracking().SingleAsync(item => item.Id == recipe.Id)).Status);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
