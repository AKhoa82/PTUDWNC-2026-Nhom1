using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeDescriptionPostgresTests
{
    [PostgresFact]
    public async Task Migration_backfills_missing_description_and_enforces_not_null()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_description_test_" + Guid.NewGuid().ToString("N");
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
            await context.GetService<IMigrator>().MigrateAsync("20261020143702_AddRecipeImageResizeDispatch");

            var author = new User { Id = Guid.NewGuid(), UserName = "description-author", FullName = "Description Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            context.Users.Add(author);
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var recipeId = Guid.NewGuid();
            const string title = "Công thức cũ thiếu mô tả";
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Recipes" ("Id", "Title", "Slug", "Description", "CategoryId", "AuthorId",
                    "CreatedAt", "PrepTimeMinutes", "CookingTimeMinutes", "Servings", "Difficulty", "Status", "Instructions")
                VALUES ({recipeId}, {title}, {"cong-thuc-cu-thieu-mo-ta"}, NULL, {category.Id}, {author.Id}, {DateTime.UtcNow}, 10, 0, 2, 1, 0, '')
                """);

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();
            var migrated = await context.Recipes.SingleAsync(recipe => recipe.Id == recipeId);
            Assert.Equal(title, migrated.Description);

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Recipes\" SET \"Description\" = NULL WHERE \"Id\" = {recipeId}"));
            Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
