using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipePaginationTests
{
    [Theory]
    [InlineData("createdAt")]
    [InlineData("-createdAt")]
    [InlineData("title")]
    [InlineData("-title")]
    [InlineData("cookTime")]
    [InlineData("-cookTime")]
    [InlineData("publishedAt")]
    [InlineData("-publishedAt")]
    [InlineData("unknown")]
    public async Task TiedSortValuesHaveStableDistinctPages(string sort)
    {
        using var db = CreateDatabase();
        var handler = CreateHandler(db);
        var first = await handler.Handle(new GetRecipesQuery(Sort: sort), default);
        var last = await handler.Handle(new GetRecipesQuery(Page: 2, Sort: sort), default);

        Assert.Equal(13, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(12, first.Items.Count);
        Assert.False(first.HasPreviousPage);
        Assert.True(first.HasNextPage);
        Assert.Single(last.Items);
        Assert.True(last.HasPreviousPage);
        Assert.False(last.HasNextPage);
        var ids = first.Items.Concat(last.Items).Select(recipe => recipe.Id).ToArray();
        Assert.Equal(13, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(id => id), ids);
    }

    [Theory]
    [InlineData(3, 12)]
    [InlineData(int.MaxValue, 50)]
    public async Task OutOfRangePagesReturnEmptyItemsAndAccurateMetadata(int page, int pageSize)
    {
        using var db = CreateDatabase();
        var result = await CreateHandler(db).Handle(new GetRecipesQuery(Page: page, PageSize: pageSize), default);
        Assert.Empty(result.Items);
        Assert.Equal(13, result.TotalCount);
        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task FiltersApplyBeforeCountAndCacheSeparatesPagesAndPageSizes()
    {
        using var db = CreateDatabase();
        var handler = CreateHandler(db);
        var category = db.Categories.Single().Id;
        var query = new GetRecipesQuery(PageSize: 5, Keyword: "soup", CategoryId: category,
            Difficulty: RecipeDifficulty.Easy, MaxCookTime: 20);
        var first = await handler.Handle(query, default);
        var second = await handler.Handle(query with { Page = 2 }, default);
        var differentSize = await handler.Handle(query with { PageSize = 1 }, default);
        var cached = await handler.Handle(query, default);
        var empty = await handler.Handle(query with { Keyword = "no matching recipe" }, default);

        Assert.Equal(13, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(r => r.Id).Intersect(second.Items.Select(r => r.Id)));
        Assert.Single(differentSize.Items);
        Assert.Equal(first.Items.Select(r => r.Id), cached.Items.Select(r => r.Id));
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNextPage);
        Assert.False(empty.HasPreviousPage);
        Assert.Equal(0, (await handler.Handle(query with { MaxCookTime = 1 }, default)).TotalCount);
        Assert.Equal(0, (await handler.Handle(query with { CategoryId = Guid.NewGuid() }, default)).TotalCount);
        Assert.Equal(0, (await handler.Handle(query with { Difficulty = RecipeDifficulty.Hard }, default)).TotalCount);
    }

    [Theory]
    [InlineData(null, false, 13)]
    [InlineData("author", false, 14)]
    [InlineData("other", false, 13)]
    [InlineData("admin", true, 14)]
    public async Task CountAndItemsRespectVisibility(string? userId, bool admin, int expected)
    {
        using var db = CreateDatabase();
        var result = await CreateHandler(db).Handle(new GetRecipesQuery(PageSize: 50, CurrentUserId: userId, IsAdmin: admin), default);
        Assert.Equal(expected, result.TotalCount);
        Assert.Equal(expected, result.Items.Count);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(-1, 12)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 51)]
    public async Task InvalidPaginationIsRejected(int page, int size)
    {
        using var db = CreateDatabase();
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(db).Handle(new GetRecipesQuery(Page: page, PageSize: size), default));
    }

    private static GetRecipesQueryHandler CreateHandler(TestContext db) => new(db,
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    private static TestContext CreateDatabase()
    {
        var db = new TestContext();
        var category = new Category { Name = "Soups", Slug = "soups" };
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Insert in reverse ID order to detect missing tie-breakers.
        for (var i = 14; i >= 1; i--)
            db.Recipes.Add(new Recipe
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}"),
                Title = "Soup", Slug = $"soup-{i}", Category = category,
                CookingTimeMinutes = 20, CreatedAt = timestamp, PublishedAt = timestamp,
                Status = i == 14 ? RecipeStatus.Draft : RecipeStatus.Published,
                AuthorId = "author"
            });
        db.SaveChanges();
        return db;
    }

    private sealed class TestContext : DbContext, IApplicationDbContext
    {
        public Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("test");
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<User> Users => throw new NotSupportedException();
        public DbSet<RecipeIngredient> RecipeIngredients => throw new NotSupportedException();
        public DbSet<RecipeStep> RecipeSteps => throw new NotSupportedException();
        public DbSet<RefreshToken> RefreshTokens => throw new NotSupportedException();
        public DbSet<StoredFile> StoredFiles => throw new NotSupportedException();
        public DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations => throw new NotSupportedException();
        public DbSet<RecipeImage> RecipeImages => throw new NotSupportedException();
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Recipe>().Ignore(recipe => recipe.Steps).Ignore(recipe => recipe.Ingredients);
        }
    }
}
