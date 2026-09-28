using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeFilterTests
{
    [Fact]
    public async Task FiltersCombineBeforeCountingAndCacheSeparatesEveryCriterion()
    {
        using var db = await CreateDatabase();
        var recipes = await db.Recipes.OrderBy(r => r.Title).ToArrayAsync();
        var handler = CreateHandler(db);
        var baseline = new GetRecipesQuery(PageSize: 50);
        async Task Check(GetRecipesQuery query, params int[] indices)
        {
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var result = await handler.Handle(query, default);
                Assert.Equal(indices.Length, result.TotalCount);
                Assert.Equal(indices.Select(i => recipes[i].Id).OrderBy(id => id), result.Items.Select(r => r.Id).OrderBy(id => id));
            }
        }
        await Check(baseline, 0, 1, 2, 3);
        await Check(baseline with { CategoryId = recipes[0].CategoryId }, 0, 1, 2);
        await Check(baseline with { CategoryId = Guid.NewGuid() });
        await Check(baseline with { MaxCookTime = 0 }, 0);
        await Check(baseline with { MaxCookTime = 30 }, 0, 1, 2);
        await Check(baseline with { MinServings = 1 }, 0, 1, 2, 3);
        await Check(baseline with { MinServings = 4 }, 1, 2, 3);
        await Check(baseline with { MinServings = 5 }, 3);
        for (var level = 1; level <= 4; level++)
            await Check(baseline with { Difficulty = (RecipeDifficulty)level }, level - 1);
        await Check(baseline with { CategoryId = recipes[0].CategoryId, Difficulty = RecipeDifficulty.Medium,
            MaxCookTime = 30, MinServings = 4, Keyword = "soup" }, 1);
        var page = await handler.Handle(baseline with { MinServings = 4, MaxCookTime = 30, PageSize = 1 }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Theory]
    [InlineData(null, false, 1)]
    [InlineData("owner", false, 3)]
    [InlineData("other", false, 1)]
    [InlineData("admin", true, 3)]
    public async Task FiltersRespectVisibility(string? user, bool admin, int count)
    {
        using var db = await CreateDatabase();
        var category = await db.Categories.FirstAsync();
        db.Recipes.AddRange(new[] { RecipeStatus.Draft, RecipeStatus.Archived }.Select(status => new Recipe {
            Title = "Private", Slug = status.ToString(), Category = category, Status = status,
            AuthorId = "owner", Servings = 8, CookingTimeMinutes = 40
        }));
        await db.SaveChangesAsync();
        var result = await CreateHandler(db).Handle(new GetRecipesQuery(MinServings: 8, MaxCookTime: 40,
            CurrentUserId: user, IsAdmin: admin), default);
        Assert.Equal(count, result.TotalCount);
        Assert.Equal(count, result.Items.Count);
    }

    [Fact]
    public async Task ChangingServingsAndCookTimeInvalidatesCachedFilters()
    {
        using var db = await CreateDatabase();
        var handler = CreateHandler(db);
        var query = new GetRecipesQuery(MinServings: 8, MaxCookTime: 30);
        Assert.Empty((await handler.Handle(query, default)).Items);
        var recipe = await db.Recipes.SingleAsync(r => r.Servings == 8);
        recipe.CookingTimeMinutes = 30;
        await db.SaveChangesAsync();
        Assert.Equal(recipe.Id, Assert.Single((await handler.Handle(query, default)).Items).Id);
        recipe.Servings = 7;
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(query, default)).Items);
    }

    [Theory]
    [InlineData(-1, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, -1, null)]
    [InlineData(null, null, 999)]
    public async Task InvalidFiltersAreRejected(int? time, int? servings, int? difficulty)
    {
        using var db = await CreateDatabase();
        var behavior = new ValidationBehavior<GetRecipesQuery, int>([new GetRecipesQueryValidator()]);
        var handlerCalled = false;
        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new GetRecipesQuery(MaxCookTime: time, MinServings: servings, Difficulty: (RecipeDifficulty?)difficulty),
            _ => { handlerCalled = true; return Task.FromResult(0); }, default));
        Assert.False(handlerCalled);
    }

    private static GetRecipesQueryHandler CreateHandler(ApplicationDbContext db) => new(db,
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    private static async Task<ApplicationDbContext> CreateDatabase()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        var first = new Category { Name = "First", Slug = "first" };
        var second = new Category { Name = "Second", Slug = "second" };
        for (var i = 0; i < 4; i++)
            db.Recipes.Add(new Recipe { Title = $"{i} Soup", Slug = $"soup-{i}", Category = i == 3 ? second : first,
                Difficulty = (RecipeDifficulty)(i + 1), CookingTimeMinutes = new[] { 0, 30, 30, 40 }[i],
                PrepTimeMinutes = 100, Servings = new[] { 1, 4, 4, 8 }[i], Status = RecipeStatus.Published });
        await db.SaveChangesAsync();
        return db;
    }
}
