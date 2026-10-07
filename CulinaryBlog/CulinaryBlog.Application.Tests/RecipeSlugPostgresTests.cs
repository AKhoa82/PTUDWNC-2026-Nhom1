using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeSlugPostgresTests
{
    [PostgresFact]
    public async Task Soft_deleted_slug_is_reserved_and_create_uses_next_available_suffix()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "recipe_slug_test_" + Guid.NewGuid().ToString("N");
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
            await using var context = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await context.Database.MigrateAsync();

            var author = new User { Id = Guid.NewGuid(), UserName = "slug-author", FullName = "Slug Author" };
            var category = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet" };
            context.Users.Add(author);
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var handler = new CreateRecipeCommandHandler(context);
            CreateRecipeCommand Command() => new(new CreateRecipeRequest
            {
                Title = "Phở Bò",
                CategoryId = category.Id,
                PrepTimeMinutes = 10,
                CookingTimeMinutes = 30,
                Servings = 2,
                Instructions = "Nấu nước dùng"
            }, author.Id.ToString());

            var first = await handler.Handle(Command(), default);
            Assert.Equal("pho-bo", first.Slug);
            var deleted = await context.Recipes.SingleAsync(recipe => recipe.Id == first.Id);
            deleted.SoftDelete();
            await context.SaveChangesAsync();
            Assert.False(await context.Recipes.AnyAsync(recipe => recipe.Id == first.Id));

            context.Recipes.Add(new Recipe
            {
                Title = "Phở Bò khác", Slug = "pho-bo-2", CategoryId = category.Id,
                AuthorId = author.Id, PrepTimeMinutes = 10
            });
            await context.SaveChangesAsync();

            var second = await handler.Handle(Command(), default);
            var third = await handler.Handle(Command(), default);
            Assert.Equal("pho-bo-1", second.Slug);
            Assert.Equal("pho-bo-3", third.Slug);
            Assert.Equal(4, await context.Recipes.IgnoreQueryFilters().CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
