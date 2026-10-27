using CulinaryBlog.Application.Features.Recipes.Commands.Steps;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeStepsPostgresTests
{
    [PostgresFact]
    public async Task Delete_renumbers_default_titles_and_concurrent_adds_receive_distinct_numbers()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_steps_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = database, Pooling = false
            }.ConnectionString;
            ApplicationDbContext OpenContext() => new(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await using var context = OpenContext();
            await context.Database.MigrateAsync();

            var author = new User { Id = Guid.NewGuid(), UserName = "steps-author", FullName = "Steps Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = "Đậu hũ chiên", Slug = "dau-hu-chien-steps",
                Description = "Món đậu hũ chiên", CategoryId = category.Id, AuthorId = author.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10, Servings = 2,
                Status = RecipeStatus.Draft
            };
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, 1, "Sơ chế"));
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, 2, "Ướp gia vị", "Ướp đậu"));
            recipe.Steps.Add(RecipeStep.Create(recipe.Id, 3, "Chiên"));
            context.Users.Add(author);
            context.Categories.Add(category);
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();

            var firstId = recipe.Steps.Single(step => step.StepNumber == 1).Id;
            await new DeleteRecipeStepCommandHandler(context).Handle(new DeleteRecipeStepCommand
            {
                RecipeId = recipe.Id, StepId = firstId, AuthorId = author.Id.ToString()
            }, default);
            context.ChangeTracker.Clear();
            var remaining = await context.RecipeSteps.AsNoTracking()
                .Where(step => step.RecipeId == recipe.Id).OrderBy(step => step.StepNumber).ToListAsync();
            Assert.Equal(new[] { 1, 2 }, remaining.Select(step => step.StepNumber));
            Assert.Equal(new[] { "Ướp đậu", "Bước 2" }, remaining.Select(step => step.Title));

            async Task<int> AddAsync(string description)
            {
                await using var db = OpenContext();
                var result = await new AddRecipeStepCommandHandler(db).Handle(new AddRecipeStepCommand
                {
                    RecipeId = recipe.Id, AuthorId = author.Id.ToString(), Description = description
                }, default);
                return result.StepNumber;
            }

            var addedNumbers = await Task.WhenAll(AddAsync("Cho vào chảo"), AddAsync("Bày ra đĩa"));
            Assert.Equal(new[] { 3, 4 }, addedNumbers.Order());
            var persisted = await context.RecipeSteps.AsNoTracking()
                .Where(step => step.RecipeId == recipe.Id).OrderBy(step => step.StepNumber).ToListAsync();
            Assert.Equal(new[] { 1, 2, 3, 4 }, persisted.Select(step => step.StepNumber));
            Assert.Equal(new[] { "Ướp đậu", "Bước 2", "Bước 3", "Bước 4" },
                persisted.Select(step => step.Title));
            Assert.Equal(3, await context.RecipeCacheInvalidations.CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
