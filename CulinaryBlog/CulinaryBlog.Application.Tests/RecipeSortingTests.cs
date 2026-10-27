using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeSortingTests : IDisposable
{
    private readonly TestContext context = new();
    private readonly GetRecipesQueryHandler handler;

    public RecipeSortingTests()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Meals" };
        // Deliberately insert out of order; cook time ties cross a page boundary.
        context.Recipes.AddRange(
            Recipe(3, "Bravo", 20, category), Recipe(1, "Charlie", 30, category),
            Recipe(4, "Delta", 10, category), Recipe(2, "Alpha", 20, category),
            Recipe(5, "Hidden", 1, category, RecipeStatus.Draft));
        context.SaveChanges();
        handler = new(context);
    }

    [Theory]
    [InlineData("createdAt", "1,2,3,4")]
    [InlineData("-createdAt", "4,3,2,1")]
    [InlineData("title", "2,3,1,4")]
    [InlineData("-title", "4,1,3,2")]
    [InlineData("cookTime", "4,2,3,1")]
    [InlineData("-cookTime", "1,2,3,4")]
    [InlineData("publishedAt", "4,3,2,1")]
    [InlineData("-publishedAt", "1,2,3,4")]
    public async Task Sorts_before_paging_and_keeps_ties_stable(string sort, string expected)
    {
        var first = await handler.Handle(new(PageSize: 2, Sort: sort), default);
        var second = await handler.Handle(new(Page: 2, PageSize: 2, Sort: sort), default);
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(4, second.TotalCount);
        Assert.Equal(expected.Split(',').Select(int.Parse).Select(Id),
            first.Items.Concat(second.Items).Select(r => r.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("TITLE")]
    [InlineData("title,-cookTime")]
    [InlineData("  -createdAt  ")]
    public async Task Normalizes_sort_and_uses_latest_by_default(string? sort)
    {
        var result = await handler.Handle(new(Sort: sort!), default);
        Assert.Equal(new[] { Id(4), Id(3), Id(2), Id(1) }, result.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Sort_combines_with_filters_without_mixing_directions()
    {
        var query = new GetRecipesQuery(Keyword: "a", MaxCookTime: 20, MinServings: 2,
            CategoryId: context.Recipes.First().CategoryId, Difficulty: RecipeDifficulty.Easy, Sort: "title");
        foreach (var sort in new[] { "title", "-title", "title", "-title" })
        {
            var result = await handler.Handle(query with { Sort = sort }, default);
            var expected = sort == "title" ? new[] { Id(2), Id(3), Id(4) } : new[] { Id(4), Id(3), Id(2) };
            Assert.Equal(expected, result.Items.Select(r => r.Id));
            Assert.Equal(3, result.TotalCount);
        }
    }

    [Fact]
    public async Task Status_filter_applies_before_paging_and_does_not_expose_drafts_publicly()
    {
        var draft = await handler.Handle(new GetRecipesQuery(PageSize: 1, StatusRaw: "Draft", IsAdmin: true), default);
        var published = await handler.Handle(new GetRecipesQuery(PageSize: 1, StatusRaw: "Published", IsAdmin: true), default);
        var publicDraft = await handler.Handle(new GetRecipesQuery(StatusRaw: "Draft"), default);

        Assert.Equal(1, draft.TotalCount);
        Assert.Equal(Id(5), Assert.Single(draft.Items).Id);
        Assert.Equal(4, published.TotalCount);
        Assert.Empty(publicDraft.Items);
        Assert.Equal(0, publicDraft.TotalCount);
    }

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");
    private static Recipe Recipe(int n, string title, int minutes, Category category, RecipeStatus status = RecipeStatus.Published)
        => new() { Id = Id(n), Title = title, CookingTimeMinutes = minutes, Category = category,
            CategoryId = category.Id, Status = status, Servings = 2,
            CreatedAt = new DateTime(2026, 1, n, 0, 0, 0, DateTimeKind.Utc),
            PublishedAt = new DateTime(2026, 2, 10 - n, 0, 0, 0, DateTimeKind.Utc) };

    public void Dispose() => context.Dispose();

    private sealed class TestContext : DbContext, IApplicationDbContext
    {
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<StoredFile> StoredFiles => throw new NotSupportedException();
        public DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations => throw new NotSupportedException();
        public DbSet<User> Users => throw new NotSupportedException();
        public DbSet<RecipeImage> RecipeImages => throw new NotSupportedException();
        public DbSet<RecipeIngredient> RecipeIngredients => throw new NotSupportedException();
        public DbSet<RecipeStep> RecipeSteps => throw new NotSupportedException();
        public DbSet<WelcomeEmailOutbox> WelcomeEmailOutboxes => Set<WelcomeEmailOutbox>();
        public DbSet<RefreshToken> RefreshTokens => throw new NotSupportedException();
        public Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("test-v1");
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Recipe>().Ignore(r => r.Nutrition).Ignore(r => r.Images)
                .Ignore(r => r.Ingredients).Ignore(r => r.Steps);
        }
    }
}
