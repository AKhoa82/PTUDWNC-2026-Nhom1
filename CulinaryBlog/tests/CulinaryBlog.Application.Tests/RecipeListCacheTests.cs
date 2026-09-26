using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeListCacheTests
{
    [Fact]
    public async Task WritesRefreshAllPagesCountsAndCategoryNames()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        var category = new Category { Name = "Before", Slug = "category" };
        var first = new Recipe { Title = "A", Slug = "a", Category = category, Status = RecipeStatus.Published };
        var second = new Recipe { Title = "B", Slug = "b", Category = category, Status = RecipeStatus.Published };
        db.Recipes.AddRange(first, second);
        await db.SaveChangesAsync();
        var handler = new GetRecipesQueryHandler(db, cache);
        var query = new GetRecipesQuery(PageSize: 1, Sort: "title");
        Assert.Equal(first.Id, Assert.Single((await handler.Handle(query, default)).Items).Id);
        Assert.Equal(second.Id, Assert.Single((await handler.Handle(query with { Page = 2 }, default)).Items).Id);

        first.Status = RecipeStatus.Archived;
        category.Name = "After";
        await db.SaveChangesAsync();
        var refreshed = await handler.Handle(query, default);
        Assert.Equal(1, refreshed.TotalCount);
        Assert.Equal(second.Id, Assert.Single(refreshed.Items).Id);
        Assert.Equal("After", refreshed.Items[0].CategoryName);
        var empty = await handler.Handle(query with { Page = 2 }, default);
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.TotalCount);

        category.Name = "Renamed independently";
        await db.SaveChangesAsync();
        Assert.Equal("Renamed independently", (await handler.Handle(query, default)).Items[0].CategoryName);

        // Synchronous writers also invalidate all cached query variants.
        db.Recipes.Remove(second);
        db.SaveChanges();
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);
    }

    [Fact]
    public async Task LateOldGenerationWriteCannotResurrectOldPages()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        var recipe = new Recipe { Title = "A", Slug = "a", Status = RecipeStatus.Published,
            Category = new Category { Name = "Category", Slug = "category" } };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        var handler = new GetRecipesQueryHandler(db, cache);
        var query = new GetRecipesQuery();
        await handler.Handle(query, default);
        var oldVersion = await db.GetRecipeListVersionAsync();
        var oldKey = $"recipes:list:p1_ps12_kw_cid__max_-createdAt_anon:v{oldVersion}";
        var oldPage = await cache.GetStringAsync(oldKey);
        Assert.NotNull(oldPage);

        recipe.Status = RecipeStatus.Archived;
        await db.SaveChangesAsync();
        // A reader started before the write finishes its cache SET afterwards.
        await cache.SetStringAsync(oldKey, oldPage!);
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);

        Assert.NotEqual(oldVersion, await db.GetRecipeListVersionAsync());
    }

    [Fact]
    public async Task RedisOutageDoesNotFailWritesAndRecoveryDoesNotServeStalePages()
    {
        var cache = new FailingCache();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        var recipe = new Recipe { Title = "Soup", Slug = "soup", Status = RecipeStatus.Published,
            Category = new Category { Name = "Soup", Slug = "soup" } };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        var handler = new GetRecipesQueryHandler(db, cache);
        var query = new GetRecipesQuery();
        Assert.Equal(1, (await handler.Handle(query, default)).TotalCount);

        cache.FailReads = cache.FailWrites = true;
        recipe.Status = RecipeStatus.Archived;
        await db.SaveChangesAsync();
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);

        // Reads recover first; failed cache SET must not fail the response either.
        cache.FailReads = false;
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);
        cache.FailWrites = false;
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);
        Assert.Equal(0, (await handler.Handle(query, default)).TotalCount);
    }

    private sealed class FailingCache : IDistributedCache
    {
        private readonly IDistributedCache _inner = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        public bool FailReads { get; set; }
        public bool FailWrites { get; set; }
        public byte[]? Get(string key) => FailReads ? throw new IOException("Redis unavailable") : _inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            if (FailWrites) throw new IOException("Redis unavailable");
            _inner.Set(key, value, options);
        }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
        public void Refresh(string key) => _inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);
        public void Remove(string key) => _inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    }
}
