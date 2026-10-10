using CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;
using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.Steps;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipePublishedDeletionPostgresTests
{
    [PostgresFact]
    public async Task Published_keeps_last_items_while_draft_and_archived_can_delete_them()
    {
        await InTemporaryDatabase(async (connection, context, authorId, categoryId) =>
        {
            var published = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Published, 1, 1);
            var ingredient = await context.RecipeIngredients.SingleAsync(x => x.RecipeId == published.Id);
            var step = await context.RecipeSteps.SingleAsync(x => x.RecipeId == published.Id);

            await Assert.ThrowsAsync<DomainException>(() => DeleteIngredientAsync(context, published.Id, ingredient.Id, authorId));
            await Assert.ThrowsAsync<DomainException>(() => DeleteStepAsync(context, published.Id, step.Id, authorId));
            Assert.Equal(1, await context.RecipeIngredients.CountAsync(x => x.RecipeId == published.Id));
            Assert.Equal(1, await context.RecipeSteps.CountAsync(x => x.RecipeId == published.Id));

            foreach (var status in new[] { RecipeStatus.Draft, RecipeStatus.Archived })
            {
                var recipe = await AddRecipeAsync(context, authorId, categoryId, status, 1, 1);
                ingredient = await context.RecipeIngredients.SingleAsync(x => x.RecipeId == recipe.Id);
                step = await context.RecipeSteps.SingleAsync(x => x.RecipeId == recipe.Id);
                await DeleteIngredientAsync(context, recipe.Id, ingredient.Id, authorId);
                await DeleteStepAsync(context, recipe.Id, step.Id, authorId);
                Assert.Equal(0, await context.RecipeIngredients.CountAsync(x => x.RecipeId == recipe.Id));
                Assert.Equal(0, await context.RecipeSteps.CountAsync(x => x.RecipeId == recipe.Id));
            }

            var twoItems = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Published, 2, 2);
            ingredient = await context.RecipeIngredients.FirstAsync(x => x.RecipeId == twoItems.Id);
            step = await context.RecipeSteps.SingleAsync(x => x.RecipeId == twoItems.Id && x.StepNumber == 1);
            await DeleteIngredientAsync(context, twoItems.Id, ingredient.Id, authorId);
            await DeleteStepAsync(context, twoItems.Id, step.Id, authorId);
            Assert.Equal(1, await context.RecipeIngredients.CountAsync(x => x.RecipeId == twoItems.Id));
            Assert.Equal(1, (await context.RecipeSteps.SingleAsync(x => x.RecipeId == twoItems.Id)).StepNumber);
            Assert.Equal(2, await context.RecipeCacheInvalidations.CountAsync(x => x.RecipeSlug == twoItems.Slug));
        });
    }

    [PostgresFact]
    public async Task Concurrent_deletes_of_two_items_leave_one_for_published_recipe()
    {
        await InTemporaryDatabase(async (connection, context, authorId, categoryId) =>
        {
            var ingredientRecipe = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Published, 2, 1);
            var ingredientIds = await context.RecipeIngredients.Where(x => x.RecipeId == ingredientRecipe.Id)
                .Select(x => x.Id).ToArrayAsync();
            var ingredientResults = await RunConcurrently(
                () => WithContext(connection, db => DeleteIngredientAsync(db, ingredientRecipe.Id, ingredientIds[0], authorId)),
                () => WithContext(connection, db => DeleteIngredientAsync(db, ingredientRecipe.Id, ingredientIds[1], authorId)));
            Assert.Single(ingredientResults, error => error is null);
            Assert.Single(ingredientResults, error => error is DomainException);
            Assert.Equal(1, await context.RecipeIngredients.CountAsync(x => x.RecipeId == ingredientRecipe.Id));

            var stepRecipe = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Published, 1, 2);
            var stepIds = await context.RecipeSteps.Where(x => x.RecipeId == stepRecipe.Id)
                .OrderBy(x => x.StepNumber).Select(x => x.Id).ToArrayAsync();
            var stepResults = await RunConcurrently(
                () => WithContext(connection, db => DeleteStepAsync(db, stepRecipe.Id, stepIds[0], authorId)),
                () => WithContext(connection, db => DeleteStepAsync(db, stepRecipe.Id, stepIds[1], authorId)));
            Assert.Single(stepResults, error => error is null);
            Assert.Single(stepResults, error => error is DomainException);
            Assert.Equal(1, await context.RecipeSteps.CountAsync(x => x.RecipeId == stepRecipe.Id));
            Assert.Equal(1, (await context.RecipeSteps.AsNoTracking().SingleAsync(x => x.RecipeId == stepRecipe.Id)).StepNumber);
        });
    }

    [PostgresFact]
    public async Task Publish_and_delete_last_step_preserve_publication_requirement()
    {
        await InTemporaryDatabase(async (connection, context, authorId, categoryId) =>
        {
            var recipe = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Draft, 1, 1);
            var stepId = await context.RecipeSteps.Where(x => x.RecipeId == recipe.Id)
                .Select(x => x.Id).SingleAsync();

            var results = await RunConcurrently(
                () => WithContext(connection, db => PublishAsync(db, recipe.Id, authorId)),
                () => WithContext(connection, db => DeleteStepAsync(db, recipe.Id, stepId, authorId)));

            Assert.Single(results, error => error is null);
            Assert.Single(results, error => error is DomainException);
            var persisted = await context.Recipes.AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
            var stepCount = await context.RecipeSteps.CountAsync(x => x.RecipeId == recipe.Id);
            Assert.True((persisted.Status == RecipeStatus.Published && stepCount == 1) ||
                        (persisted.Status == RecipeStatus.Draft && stepCount == 0));
            Assert.Equal(1, await context.RecipeIngredients.CountAsync(x => x.RecipeId == recipe.Id));
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync(x => x.RecipeSlug == recipe.Slug));
        });
    }

    [PostgresFact]
    public async Task Publish_and_delete_last_ingredient_preserve_publication_requirement()
    {
        await InTemporaryDatabase(async (connection, context, authorId, categoryId) =>
        {
            var recipe = await AddRecipeAsync(context, authorId, categoryId, RecipeStatus.Draft, 1, 1);
            var ingredientId = await context.RecipeIngredients.Where(x => x.RecipeId == recipe.Id)
                .Select(x => x.Id).SingleAsync();

            var results = await RunConcurrently(
                () => WithContext(connection, db => PublishAsync(db, recipe.Id, authorId)),
                () => WithContext(connection, db => DeleteIngredientAsync(db, recipe.Id, ingredientId, authorId)));

            Assert.Single(results, error => error is null);
            Assert.Single(results, error => error is DomainException);
            var persisted = await context.Recipes.AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
            var ingredientCount = await context.RecipeIngredients.CountAsync(x => x.RecipeId == recipe.Id);
            Assert.True((persisted.Status == RecipeStatus.Published && ingredientCount == 1) ||
                        (persisted.Status == RecipeStatus.Draft && ingredientCount == 0));
            Assert.Equal(1, await context.RecipeSteps.CountAsync(x => x.RecipeId == recipe.Id));
            Assert.Equal(1, await context.RecipeCacheInvalidations.CountAsync(x => x.RecipeSlug == recipe.Slug));
        });
    }

    private static async Task InTemporaryDatabase(
        Func<string, ApplicationDbContext, Guid, Guid, Task> test)
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
            await using var context = CreateContext(connection);
            await context.Database.MigrateAsync();
            var author = new User { Id = Guid.NewGuid(), UserName = "delete-author", FullName = "Delete Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            context.Users.Add(author);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            await test(connection, context, author.Id, category.Id);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ApplicationDbContext CreateContext(string connection) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    private static async Task<Recipe> AddRecipeAsync(
        ApplicationDbContext context, Guid authorId, Guid categoryId, RecipeStatus status,
        int ingredientCount, int stepCount)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), Title = "Công thức thử", Slug = Guid.NewGuid().ToString("N"),
            AuthorId = authorId, CategoryId = categoryId, Status = status, PrepTimeMinutes = 10
        };
        for (var i = 0; i < ingredientCount; i++)
            recipe.Ingredients.Add(RecipeIngredient.Create(recipe.Id, $"Nguyên liệu {i + 1}", 1, "g", null, i));
        for (var i = 0; i < stepCount; i++)
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, i + 1, $"Bước {i + 1}"));
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
        return recipe;
    }

    private static Task DeleteIngredientAsync(ApplicationDbContext context, Guid recipeId, Guid ingredientId, Guid authorId)
        => new DeleteIngredientCommandHandler(context).Handle(
            new DeleteIngredientCommand(recipeId, ingredientId, authorId.ToString(), false), default);

    private static Task DeleteStepAsync(ApplicationDbContext context, Guid recipeId, Guid stepId, Guid authorId)
        => new DeleteRecipeStepCommandHandler(context).Handle(new DeleteRecipeStepCommand
        {
            RecipeId = recipeId, StepId = stepId, AuthorId = authorId.ToString()
        }, default);

    private static Task PublishAsync(ApplicationDbContext context, Guid recipeId, Guid authorId)
        => new PublishRecipeCommandHandler(context).Handle(
            new PublishRecipeCommand(recipeId, authorId.ToString(), false), default);

    private static async Task WithContext(string connection, Func<ApplicationDbContext, Task> action)
    {
        await using var context = CreateContext(connection);
        await action(context);
    }

    private static async Task<Exception?[]> RunConcurrently(Func<Task> first, Func<Task> second)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Run(Func<Task> action)
        {
            await start.Task;
            try { await action(); return null; }
            catch (Exception error) { return error; }
        }
        var tasks = new[] { Task.Run(() => Run(first)), Task.Run(() => Run(second)) };
        start.SetResult();
        return await Task.WhenAll(tasks);
    }
}
