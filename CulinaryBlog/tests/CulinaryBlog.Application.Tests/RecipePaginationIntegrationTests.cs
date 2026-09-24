using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipePaginationIntegrationTests
{
    private sealed class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("RUN_PAGINATION_INTEGRATION") != "1")
                Skip = "Set RUN_PAGINATION_INTEGRATION=1 with local PostgreSQL and Redis running.";
        }
    }

    [IntegrationFact]
    public async Task PostgreSqlAndRedisReturnFreshStablePagesAfterWrites()
    {
        var runId = Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("PAGINATION_TEST_POSTGRES") ??
            "Host=localhost;Port=5432;Username=postgres;Password=postgres")
        {
            Database = $"pagination_test_{runId}"
        };
        using var cache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = Environment.GetEnvironmentVariable("PAGINATION_TEST_REDIS") ?? "localhost:6379",
            InstanceName = $"pagination-test:{runId}:"
        }));
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var category = new Category { Name = "Test", Slug = "test" };
            var timestamp = DateTime.UtcNow;
            var recipes = Enumerable.Range(1, 13).Select(i => new Recipe
            {
                Title = "Soup", Slug = $"soup-{i}", Category = category,
                CreatedAt = timestamp, Status = RecipeStatus.Published
            }).ToArray();
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
            var handler = new GetRecipesQueryHandler(db, cache);
            var query = new GetRecipesQuery(Keyword: "soup");
            var first = await handler.Handle(query, default);
            var last = await handler.Handle(query with { Page = 2 }, default);
            Assert.Equal(13, first.TotalCount);
            Assert.Equal(12, first.Items.Count);
            Assert.Single(last.Items);
            Assert.Equal(13, first.Items.Concat(last.Items).Select(item => item.Id).Distinct().Count());
            Assert.Empty((await handler.Handle(query with { Page = int.MaxValue, PageSize = 50 }, default)).Items);
            Assert.Equal(first.Items.Select(item => item.Id),
                (await handler.Handle(query, default)).Items.Select(item => item.Id));

            recipes[0].Status = RecipeStatus.Archived;
            await db.SaveChangesAsync();
            Assert.Equal(12, (await handler.Handle(query, default)).TotalCount);
            Assert.Empty((await handler.Handle(query with { Page = 2 }, default)).Items);

            using var reader = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection.ConnectionString).Options);
            var readerHandler = new GetRecipesQueryHandler(reader, cache);
            var committedVersion = await reader.GetRecipeListVersionAsync();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                recipes[1].Status = RecipeStatus.Archived;
                await db.SaveChangesAsync();
                Assert.NotEqual(committedVersion, await db.GetRecipeListVersionAsync());
                Assert.Equal(committedVersion, await reader.GetRecipeListVersionAsync());
                Assert.Equal(12, (await readerHandler.Handle(query, default)).TotalCount);
                await transaction.RollbackAsync();
            }
            Assert.Equal(committedVersion, await reader.GetRecipeListVersionAsync());
            Assert.Equal(12, (await readerHandler.Handle(query, default)).TotalCount);

            // A transaction committed after SaveChanges must expose data and generation together.
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                recipes[2].Status = RecipeStatus.Archived;
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            Assert.NotEqual(committedVersion, await reader.GetRecipeListVersionAsync());
            Assert.Equal(11, (await readerHandler.Handle(query, default)).TotalCount);
        }
        finally
        {
            // Only the uniquely named database created by this test is removed.
            await db.Database.EnsureDeletedAsync();
            // Page keys have a 15 minute TTL and an isolated prefix.
        }
    }
}
