using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class CreateRecipeAuthorizationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-user")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("33333333-3333-3333-3333-333333333333")]
    public async Task HandlerRejectsInvalidOrMissingActor(string? actor)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new CreateRecipeCommandHandler(db)
            .Handle(new CreateRecipeCommand(new CreateRecipeRequest(), actor), default));
        Assert.Empty(db.Recipes);
        Assert.Empty(db.RecipeCacheInvalidations);
    }

    [Fact]
    public async Task RolelessExistingUserCanCreateAndWritesCacheIntentAtomically()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Id = Guid.NewGuid(), UserName = "legacy" };
        var category = new Category { Name = "Food", Slug = "food" };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var request = new CreateRecipeRequest { Title = "Dinner", Instructions = "Cook", Servings = 1, CategoryId = category.Id };
        var handler = new CreateRecipeCommandHandler(db);
        var id = await handler.Handle(new CreateRecipeCommand(request, user.Id.ToString()), default);
        Assert.Equal(user.Id.ToString(), (await db.Recipes.SingleAsync(x => x.Id == id)).AuthorId);
        Assert.Single(db.RecipeCacheInvalidations);
        request.ImageUrl = new string('x', 501);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new CreateRecipeCommand(request, user.Id.ToString()), default));
        Assert.Single(db.Recipes);
    }
}
