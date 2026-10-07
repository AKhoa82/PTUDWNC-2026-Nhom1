using CulinaryBlog.Application.Features.Recipes.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")))
            Skip = "Set SEARCH_TEST_POSTGRES to a PostgreSQL connection with CREATE DATABASE permission.";
    }
}

public class RecipeSearchPostgresTests
{
    [PostgresFact]
    public async Task Migration_backfill_trigger_rank_visibility_and_pagination_work_on_Postgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "search_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false };
            await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(builder.ConnectionString).Options);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260929062806_UpdateDomainException");
            // Insert before the new column exists to exercise the migration's backfill.
            var category = Guid.NewGuid();
            var first = Guid.NewGuid();
            var author = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AspNetUsers" ("Id", "Username", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "FullName", "CreatedAt")
                VALUES ({author}, 'test', 'TEST', 'test@test.com', 'TEST@TEST.COM', false, '', '', '', '', false, false, null, false, 0, 'Test User', now());
                INSERT INTO "Categories" ("Id", "Name", "Slug", "Description", "CreatedAt")
                VALUES ({category}, 'Test', 'test', '', now());
                INSERT INTO "Recipes" ("Id", "Title", "Slug", "Description", "CategoryId", "AuthorId", "CreatedAt",
                    "PrepTimeMinutes", "CookingTimeMinutes", "Servings", "Difficulty", "Status", "Instructions")
                VALUES ({first}, 'Phở bò', 'pho-bo', 'Nước dùng ngon', {category}, {author.ToString()}, now(), 1, 30, 2, 1, 1, '');
                """);
            await migrator.MigrateAsync();
            var search = new RecipeSearchService(context);
            var backfilled = await search.SearchAsync(new("pho bo"), default);
            Assert.Equal(first, Assert.Single(backfilled.Items).Id);
            Assert.True(backfilled.Items[0].RelevanceScore > 0);

            var descriptionMatch = new Recipe { Title = "Món ngon", Slug = "mon-ngon", Description = "Phở bò",
                CategoryId = category, AuthorId = author, Status = RecipeStatus.Published, PrepTimeMinutes = 1 };
            var draft = new Recipe { Title = "Phở bò", Slug = "draft", CategoryId = category, AuthorId = author, Status = RecipeStatus.Draft, PrepTimeMinutes = 1 };
            var archived = new Recipe { Title = "Phở bò", Slug = "archived", CategoryId = category, AuthorId = author, Status = RecipeStatus.Archived, PrepTimeMinutes = 1 };
            context.Recipes.AddRange(descriptionMatch, draft, archived);
            await context.SaveChangesAsync();
            foreach (var term in new[] { "pho bo", "PHỞ BÒ", "ph b", "pho | bo" })
            {
                var result = await search.SearchAsync(new(term, 1, 1), default);
                Assert.Equal(2, result.TotalCount);
                Assert.Equal(first, Assert.Single(result.Items).Id);
                Assert.True(result.HasNextPage);
            }
            var second = await search.SearchAsync(new("pho bo", 2, 1), default);
            Assert.Equal(descriptionMatch.Id, Assert.Single(second.Items).Id);
            descriptionMatch.Description = "Bánh mì";
            await context.SaveChangesAsync();
            Assert.Single((await search.SearchAsync(new("pho bo"), default)).Items);
            Assert.Equal(descriptionMatch.Id, Assert.Single((await search.SearchAsync(new("banh mi"), default)).Items).Id);
            descriptionMatch.Title = "Đậu hũ";
            descriptionMatch.Description = null;
            await context.SaveChangesAsync();
            Assert.Equal(descriptionMatch.Id, Assert.Single((await search.SearchAsync(new("dau hu"), default)).Items).Id);
            var empty = await search.SearchAsync(new("zzzz"), default);
            Assert.Empty(empty.Items);
            Assert.NotNull(empty.Message);
            Assert.Empty((await search.SearchAsync(new("pho bo", int.MaxValue, 50), default)).Items);
            Assert.Empty((await search.SearchAsync(new("pho'); DROP TABLE Recipes;--"), default)).Items);
            var tied = new Recipe { Title = "Phở bò", Description = "Nước dùng ngon", Slug = "pho-bo-2",
                CategoryId = category, AuthorId = author, Status = RecipeStatus.Published, PrepTimeMinutes = 1 };
            context.Recipes.Add(tied);
            await context.SaveChangesAsync();
            var tiedPage1 = await search.SearchAsync(new("pho bo", 1, 1), default);
            var tiedPage2 = await search.SearchAsync(new("pho bo", 2, 1), default);
            var expectedIds = new[] { first, tied.Id }.OrderBy(id => id).ToArray();
            Assert.Equal(expectedIds, new[] { tiedPage1.Items[0].Id, tiedPage2.Items[0].Id });
            Assert.Equal(tiedPage1.Items[0].RelevanceScore, tiedPage2.Items[0].RelevanceScore);

            // Test Filters
            var category2 = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Categories" ("Id", "Name", "Slug", "Description", "CreatedAt")
                VALUES ({category2}, 'Test 2', 'test-2', '', now());
                """);
            var filtered1 = new Recipe { Title = "Phở cuốn", Slug = "pho-cuon", Description = "Ngon",
                CategoryId = category, AuthorId = author, Status = RecipeStatus.Published, Difficulty = RecipeDifficulty.Hard, PrepTimeMinutes = 10, CookingTimeMinutes = 15, Servings = 2 };
            var filtered2 = new Recipe { Title = "Phở xào", Slug = "pho-xao", Description = "Ngon",
                CategoryId = category2, AuthorId = author, Status = RecipeStatus.Published, Difficulty = RecipeDifficulty.Expert, PrepTimeMinutes = 10, CookingTimeMinutes = 45, Servings = 4 };
            context.Recipes.AddRange(filtered1, filtered2);
            await context.SaveChangesAsync();

            Assert.Equal(filtered1.Id, Assert.Single((await search.SearchAsync(new("pho", CategoryId: category, Difficulty: "Hard"), default)).Items).Id);
            Assert.Equal(filtered2.Id, Assert.Single((await search.SearchAsync(new("pho", CategoryId: category2, Difficulty: "Expert"), default)).Items).Id);
            Assert.DoesNotContain((await search.SearchAsync(new("pho", MaxCookTime: 40), default)).Items, r => r.Id == filtered2.Id);
            Assert.Contains((await search.SearchAsync(new("pho", MaxCookTime: 50), default)).Items, r => r.Id == filtered2.Id);
            Assert.Equal(filtered2.Id, Assert.Single((await search.SearchAsync(new("pho", MinServings: 4), default)).Items).Id);

            // Test Sort
            var sortedDesc = await search.SearchAsync(new("pho", Sort: "-cooktime"), default);
            Assert.Equal(filtered2.Id, sortedDesc.Items.First().Id);

            var sortedAsc = await search.SearchAsync(new("pho", Sort: "cooktime"), default);
            Assert.Equal(filtered1.Id, sortedAsc.Items.First(i => i.CookingTimeMinutes > 0).Id);
            // Rollback removes only feature-owned objects and supports reapplying the migration.
            await migrator.MigrateAsync("20260929062806_UpdateDomainException");
            await migrator.MigrateAsync();
            Assert.Equal(2, (await search.SearchAsync(new("pho bo"), default)).Items.Count);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
