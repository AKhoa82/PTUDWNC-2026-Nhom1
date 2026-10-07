using System.Text.Json;
using CulinaryBlog.API.Infrastructure;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;
using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.Steps;
using CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Application.Tests;

[Trait("Suite", "RecipeSrs")]
public sealed class RecipeRequirementTests
{
    // SRS FR-RCP-003 and conflict resolution 17: prep time and servings > 0,
    // cooking time >= 0; title has at least five characters.
    [Theory]
    [InlineData("Phở", 10, 0, 2)]
    [InlineData("Phở bò Hà Nội", 0, 0, 2)]
    [InlineData("Phở bò Hà Nội", 10, -1, 2)]
    [InlineData("Phở bò Hà Nội", 10, 0, 0)]
    public void Create_rejects_invalid_core_fields(string title, int prep, int cook, int servings)
    {
        var request = ValidCreateRequest();
        request.Title = title;
        request.PrepTimeMinutes = prep;
        request.CookingTimeMinutes = cook;
        request.Servings = servings;

        Assert.False(new CreateRecipeCommandValidator()
            .Validate(new CreateRecipeCommand(request, Guid.NewGuid().ToString())).IsValid);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Create_rejects_invalid_nested_content(bool validIngredient, bool validStep)
    {
        var request = ValidCreateRequest();
        request.Ingredients.Add(new CreateIngredientRequestDto { Name = validIngredient ? "Muối" : "" });
        request.Steps.Add(new CreateStepRequestDto { Description = validStep ? "Nêm gia vị" : "" });

        Assert.False(new CreateRecipeCommandValidator()
            .Validate(new CreateRecipeCommand(request, Guid.NewGuid().ToString())).IsValid);
    }

    [Fact]
    public void Create_accepts_valid_no_cook_recipe()
    {
        var request = ValidCreateRequest();
        Assert.True(new CreateRecipeCommandValidator()
            .Validate(new CreateRecipeCommand(request, Guid.NewGuid().ToString())).IsValid);
    }

    [Fact]
    public void Create_rejects_undefined_difficulty()
    {
        var request = ValidCreateRequest();
        request.Difficulty = (RecipeDifficulty)999;

        Assert.False(new CreateRecipeCommandValidator()
            .Validate(new CreateRecipeCommand(request, Guid.NewGuid().ToString())).IsValid);
    }

    // Conflict resolution 8: all six nutrition fields must survive create/read.
    [Fact]
    public void Create_request_accepts_six_nutrition_fields()
    {
        const string json = """
            {"title":"Phở bò Hà Nội","categoryId":"11111111-1111-1111-1111-111111111111",
             "nutrition":{"calories":300,"protein":20,"carbs":30,"fat":10,"fiber":2,"sodium":450}}
            """;
        var request = JsonSerializer.Deserialize<CreateRecipeRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(serialized.RootElement.TryGetProperty("nutrition", out var nutrition));
        Assert.Equal(2, nutrition.GetProperty("fiber").GetInt32());
        Assert.Equal(450, nutrition.GetProperty("sodium").GetInt32());
    }

    [Fact]
    public async Task Detail_returns_six_nutrition_fields()
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe();
        recipe.SetNutrition(300, 20, 30, 10, 2, 450);
        await db.SaveChangesAsync();

        var detail = await new GetRecipeBySlugQueryHandler(db)
            .Handle(new GetRecipeBySlugQuery(recipe.Slug), default);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(detail,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("nutrition", out var nutrition));
        Assert.Equal(2, nutrition.GetProperty("fiber").GetDecimal());
        Assert.Equal(450, nutrition.GetProperty("sodium").GetDecimal());
    }

    [Fact]
    public void Publish_again_keeps_original_publication_time()
    {
        var recipe = new Recipe { Status = RecipeStatus.Published,
            PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        recipe.Steps.Add(RecipeStep.Create(Guid.NewGuid(), 1, "Nấu"));
        recipe.Ingredients.Add(RecipeIngredient.Create(Guid.NewGuid(), "Muối", null, null, null, 0));

        recipe.Publish();

        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), recipe.PublishedAt);
    }

    [Fact]
    public void Unpublish_does_not_unarchive_recipe()
    {
        var recipe = new Recipe { Status = RecipeStatus.Archived };
        recipe.Unpublish();
        Assert.Equal(RecipeStatus.Archived, recipe.Status);
    }

    // A cached public detail must be evicted after every visible mutation.
    [Theory]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    [InlineData("ingredient-add")]
    [InlineData("ingredient-update")]
    [InlineData("ingredient-delete")]
    [InlineData("step-add")]
    [InlineData("step-update")] 
    [InlineData("step-delete")]
    public async Task Recipe_mutation_queues_detail_cache_invalidation(string operation)
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe(operation == "publish" ? RecipeStatus.Draft : RecipeStatus.Published);
        var userId = recipe.AuthorId.ToString();
        var ingredient = recipe.Ingredients.Single();

        switch (operation)
        {
            case "publish":
                await new PublishRecipeCommandHandler(db)
                    .Handle(new PublishRecipeCommand(recipe.Id, userId, false), default);
                break;
            case "unpublish":
                await new UnpublishRecipeCommandHandler(db)
                    .Handle(new UnpublishRecipeCommand(recipe.Id, userId, false), default);
                break;
            case "archive":
                await new ArchiveRecipeCommandHandler(db)
                    .Handle(new ArchiveRecipeCommand(recipe.Id, userId, false), default);
                break;
            case "ingredient-add":
                await new AddIngredientCommandHandler(db).Handle(
                    new AddIngredientCommand(recipe.Id, "Muối", null, null, null, 1, userId, false), default);
                break;
            case "ingredient-update":
                await new UpdateIngredientCommandHandler(db).Handle(
                    new UpdateIngredientCommand(recipe.Id, ingredient.Id, "Thịt bò tươi", 200, "g", null, 0, userId, false), default);
                break;
            case "ingredient-delete":
                await new DeleteIngredientCommandHandler(db).Handle(
                    new DeleteIngredientCommand(recipe.Id, ingredient.Id, userId, false), default);
                break;
            case "step-add":
                await new AddRecipeStepCommandHandler(db).Handle(new AddRecipeStepCommand
                {
                    RecipeId = recipe.Id, AuthorId = userId, Description = "Bày ra tô"
                }, default);
                break;
            case "step-update":
                await new UpdateRecipeStepCommandHandler(db).Handle(new UpdateRecipeStepCommand
                {
                    RecipeId = recipe.Id, StepId = recipe.Steps.Single().Id,
                    AuthorId = userId, Description = "Nấu nước dùng thật lâu"
                }, default);
                break;
            case "step-delete":
                await new DeleteRecipeStepCommandHandler(db).Handle(new DeleteRecipeStepCommand
                {
                    RecipeId = recipe.Id, StepId = recipe.Steps.Single().Id,
                    AuthorId = userId, IsAdmin = false
                }, default);
                break;
        }

        Assert.Single(db.RecipeCacheInvalidations);
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    [InlineData("delete")]
    [InlineData("step-add")]
    [InlineData("step-update")]
    [InlineData("step-delete")]
    [InlineData("ingredient-add")]
    [InlineData("ingredient-update")]
    [InlineData("ingredient-delete")]
    public async Task Authenticated_non_owner_receives_forbidden_error(string operation)
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe(operation == "publish" ? RecipeStatus.Draft : RecipeStatus.Published);
        var otherUserId = Guid.NewGuid().ToString();
        

        async Task Execute()
        {
            switch (operation)
            {
                case "publish":
                    await new PublishRecipeCommandHandler(db).Handle(
                        new PublishRecipeCommand(recipe.Id, otherUserId, false), default);
                    break;
                case "unpublish":
                    await new UnpublishRecipeCommandHandler(db).Handle(
                        new UnpublishRecipeCommand(recipe.Id, otherUserId, false), default);
                    break;
                case "archive":
                    await new ArchiveRecipeCommandHandler(db).Handle(
                        new ArchiveRecipeCommand(recipe.Id, otherUserId, false), default);
                    break;
                case "delete":
                    await new CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe.DeleteRecipeCommandHandler(
                        db, NullLogger<CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe.DeleteRecipeCommandHandler>.Instance).Handle(
                        new CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe.DeleteRecipeCommand(
                            recipe.Id, otherUserId, false), default);
                    break;
                case "ingredient-add":
                    await new AddIngredientCommandHandler(db).Handle(
                        new AddIngredientCommand(recipe.Id, "Muối", null, null, null, 1, otherUserId, false), default);
                    break;
                case "ingredient-update":
                    await new UpdateIngredientCommandHandler(db).Handle(
                        new UpdateIngredientCommand(recipe.Id, recipe.Ingredients.First().Id,
                            "Muối", null, null, null, 1, otherUserId, false), default);
                    break;
                case "ingredient-delete":
                    await new DeleteIngredientCommandHandler(db).Handle(
                        new DeleteIngredientCommand(recipe.Id, recipe.Ingredients.First().Id, otherUserId, false), default);
                    break;
                case "step-add":
                    await new AddRecipeStepCommandHandler(db).Handle(new AddRecipeStepCommand
                    {
                        RecipeId = recipe.Id, AuthorId = otherUserId, Description = "Bày ra tô"
                    }, default);
                    break;
                case "step-update":
                    await new UpdateRecipeStepCommandHandler(db).Handle(new UpdateRecipeStepCommand
                    {
                        RecipeId = recipe.Id, StepId = recipe.Steps.Single().Id,
                        AuthorId = otherUserId, Description = "Nấu nước dùng thật lâu"
                    }, default);
                    break;
                case "step-delete":
                    await new DeleteRecipeStepCommandHandler(db).Handle(new DeleteRecipeStepCommand
                    {
                        RecipeId = recipe.Id, StepId = recipe.Steps.Single().Id,
                        AuthorId = otherUserId, IsAdmin = false
                    }, default);
                    break;
            }
        }

        await Assert.ThrowsAsync<ForbiddenException>(Execute);
        Assert.Empty(db.RecipeCacheInvalidations);
    }

    [Theory]
    [InlineData("ingredient-update")]
    [InlineData("ingredient-delete")]
    public async Task Missing_ingredient_returns_not_found(string operation)
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe();
        var userId = recipe.AuthorId.ToString();
        var missingIngId = Guid.NewGuid();

        async Task Execute()
        {
            switch (operation)
            {
                case "ingredient-update":
                    await new UpdateIngredientCommandHandler(db).Handle(
                        new UpdateIngredientCommand(recipe.Id, missingIngId, "Muối", null, null, null, 0, userId, false), default);
                    break;
                case "ingredient-delete":
                    await new DeleteIngredientCommandHandler(db).Handle(
                        new DeleteIngredientCommand(recipe.Id, missingIngId, userId, false), default);
                    break;
            }
        }

        await Assert.ThrowsAsync<NotFoundException>(Execute);
        Assert.Empty(db.RecipeCacheInvalidations);
    }

        [Theory]
    [InlineData("step-add")]
    [InlineData("step-update")]
    [InlineData("step-delete")]
    public async Task Missing_recipe_in_step_mutation_returns_not_found(string operation)
    {
        await using var db = new RecipeAcceptanceContext();
        var missingId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        async Task Execute()
        {
            switch (operation)
            {
                case "step-add":
                    await new AddRecipeStepCommandHandler(db).Handle(new AddRecipeStepCommand { RecipeId = missingId, AuthorId = userId, Description = "Bày ra tô" }, default);
                    break;
                case "step-update":
                    await new UpdateRecipeStepCommandHandler(db).Handle(new UpdateRecipeStepCommand { RecipeId = missingId, StepId = Guid.NewGuid(), AuthorId = userId, Description = "Nấu" }, default);
                    break;
                case "step-delete":
                    await new DeleteRecipeStepCommandHandler(db).Handle(new DeleteRecipeStepCommand { RecipeId = missingId, StepId = Guid.NewGuid(), AuthorId = userId, IsAdmin = false }, default);
                    break;
            }
        }

        await Assert.ThrowsAsync<NotFoundException>(Execute);
        Assert.Empty(db.RecipeCacheInvalidations);
    }

    [Theory]
    [InlineData("step-update")]
    [InlineData("step-delete")]
    public async Task Missing_step_returns_not_found(string operation)
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe();
        var userId = recipe.AuthorId.ToString();
        var missingStepId = Guid.NewGuid();

        async Task Execute()
        {
            switch (operation)
            {
                case "step-update":
                    await new UpdateRecipeStepCommandHandler(db).Handle(new UpdateRecipeStepCommand { RecipeId = recipe.Id, StepId = missingStepId, AuthorId = userId, Description = "Nấu" }, default);
                    break;
                case "step-delete":
                    await new DeleteRecipeStepCommandHandler(db).Handle(new DeleteRecipeStepCommand { RecipeId = recipe.Id, StepId = missingStepId, AuthorId = userId, IsAdmin = false }, default);
                    break;
            }
        }

        await Assert.ThrowsAsync<NotFoundException>(Execute);
        Assert.Empty(db.RecipeCacheInvalidations);
    }

    [Fact]
    public async Task Invalid_image_returns_client_error()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/recipes/11111111-1111-1111-1111-111111111111/images";
        http.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(http, new CulinaryBlog.Application.Features.Recipes.Images.RecipeImageValidationException("File không hợp lệ."), default);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, http.Response.StatusCode);
    }

    [Fact]
    public async Task ValidateImageFileAsync_empty_image_throws()
    {
        var fileMock = new FormFile(new MemoryStream(), 0, 0, "file", "test.jpg");
        await Assert.ThrowsAsync<CulinaryBlog.Application.Features.Recipes.Images.RecipeImageValidationException>(() => FileValidationHelper.ValidateImageFileAsync(fileMock));
    }

    [Fact]
    public async Task ValidateImageFileAsync_large_image_throws()
    {
        var fileMock = new FormFile(new MemoryStream(new byte[FileValidationHelper.MaxFileSizeBytes + 1]), 0, FileValidationHelper.MaxFileSizeBytes + 1, "file", "test.jpg");
        await Assert.ThrowsAsync<CulinaryBlog.Application.Features.Recipes.Images.RecipeImageValidationException>(() => FileValidationHelper.ValidateImageFileAsync(fileMock));
    }

    [Fact]
    public async Task ValidateImageFileAsync_bad_mime_throws()
    {
        var stream = new MemoryStream(new byte[] { 0xff, 0xd8, 0xff, 0x00 });
        var fileMock = new FormFile(stream, 0, stream.Length, "file", "test.jpg") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        await Assert.ThrowsAsync<CulinaryBlog.Application.Features.Recipes.Images.RecipeImageValidationException>(() => FileValidationHelper.ValidateImageFileAsync(fileMock));
    }

    [Fact]
    public async Task ValidateImageFileAsync_bad_magic_bytes_throws()
    {
        var stream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        var fileMock = new FormFile(stream, 0, stream.Length, "file", "test.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        await Assert.ThrowsAsync<CulinaryBlog.Application.Features.Recipes.Images.RecipeImageValidationException>(() => FileValidationHelper.ValidateImageFileAsync(fileMock));
    }

    private static CreateRecipeRequest ValidCreateRequest() => new()
    {
        Title = "Phở bò Hà Nội", CategoryId = Guid.NewGuid(),
        PrepTimeMinutes = 10, CookingTimeMinutes = 0, Servings = 2,
        Instructions = "Nấu nước dùng"
    };
    [Fact]
    public async Task Create_returns_RecipeDto()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        db.Users.Add(new User { Id = authorId, UserName = "testuser", FullName = "Test User" });
        var category = new Category { Id = Guid.NewGuid(), Name = "Test Category", Slug = "test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var request = ValidCreateRequest();
        request.CategoryId = category.Id;

        var handler = new CreateRecipeCommandHandler(db);
        var result = await handler.Handle(new CreateRecipeCommand(request, authorId.ToString()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.IsType<RecipeDto>(result);
        Assert.Equal(RecipeStatus.Draft.ToString(), result.Status);
        Assert.NotEmpty(result.Slug);
        Assert.Equal(category.Name, result.CategoryName);
    }

    [Fact]
    public async Task Create_duplicate_title_preserves_slug_suffix()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        db.Users.Add(new User { Id = authorId, UserName = "testuser", FullName = "Test User" });
        var category = new Category { Id = Guid.NewGuid(), Name = "Test Category", Slug = "test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var request = ValidCreateRequest();
        request.CategoryId = category.Id;
        request.Title = "Duplicate Title";

        var handler = new CreateRecipeCommandHandler(db);
        var result1 = await handler.Handle(new CreateRecipeCommand(request, authorId.ToString()), CancellationToken.None);
        var result2 = await handler.Handle(new CreateRecipeCommand(request, authorId.ToString()), CancellationToken.None);

        Assert.Equal("duplicate-title", result1.Slug);
        Assert.Equal("duplicate-title-1", result2.Slug);
    }

    [Fact]
    public async Task Create_with_Nutrition_maps_all_6_fields()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        db.Users.Add(new User { Id = authorId, UserName = "testuser", FullName = "Test User" });
        var category = new Category { Id = Guid.NewGuid(), Name = "Test Category", Slug = "test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var request = ValidCreateRequest();
        request.CategoryId = category.Id;
        request.Nutrition = new RecipeNutritionDto { Calories = 100, Protein = 10, Carbs = 20, Fat = 5, Fiber = 2, Sodium = 300 };

        var handler = new CreateRecipeCommandHandler(db);
        var result = await handler.Handle(new CreateRecipeCommand(request, authorId.ToString()), CancellationToken.None);

        Assert.NotNull(result.Nutrition);
        Assert.Equal(100, result.Nutrition.Calories);
        Assert.Equal(10, result.Nutrition.Protein);
        Assert.Equal(20, result.Nutrition.Carbs);
        Assert.Equal(5, result.Nutrition.Fat);
        Assert.Equal(2, result.Nutrition.Fiber);
        Assert.Equal(300, result.Nutrition.Sodium);
    }

    [Fact]
    public async Task Create_without_Nutrition_works()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        db.Users.Add(new User { Id = authorId, UserName = "testuser", FullName = "Test User" });
        var category = new Category { Id = Guid.NewGuid(), Name = "Test Category", Slug = "test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var request = ValidCreateRequest();
        request.CategoryId = category.Id;
        request.Nutrition = null;

        var handler = new CreateRecipeCommandHandler(db);
        var result = await handler.Handle(new CreateRecipeCommand(request, authorId.ToString()), CancellationToken.None);

        Assert.Null(result.Nutrition);
    }

    [Fact]
    public void ImageUrl_over_500_validation_invalid()
    {
        var request = ValidCreateRequest();
        request.ImageUrl = new string('a', 501);
        var validator = new CreateRecipeCommandValidator();
        var result = validator.Validate(new CreateRecipeCommand(request, Guid.NewGuid().ToString()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Request.ImageUrl");
    }

    [Fact]
    public async Task Detail_returns_Author_and_null_Nutrition_when_absent()
    {
        await using var db = new RecipeAcceptanceContext();
        var recipe = db.SeedRecipe();
        var handler = new GetRecipeBySlugQueryHandler(db);
        var result = await handler.Handle(new GetRecipeBySlugQuery(recipe.Slug, null, false), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Nutrition);
        Assert.NotNull(result.Author);
        Assert.Equal(recipe.AuthorId, result.Author.Id);
        Assert.Equal("testuser", result.Author.UserName);
        Assert.Equal("Test User", result.Author.FullName);
    }

    [Fact]
    public async Task Update_returns_RecipeDto_and_changes_CategoryName()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "C1", Slug = "c1" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "C2", Slug = "c2" };
        db.Categories.AddRange(cat1, cat2);
        var recipe = new Recipe { Id = Guid.NewGuid(), Title = "A", Slug = "a", Difficulty = RecipeDifficulty.Easy, CategoryId = cat1.Id, AuthorId = authorId, Status = RecipeStatus.Draft };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var request = new UpdateRecipeRequest { Title = "A", CategoryId = cat2.Id, RowVersion = recipe.RowVersion.ToString() };
        var handler = new CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe.UpdateRecipeCommandHandler(db);
        var result = await handler.Handle(new CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe.UpdateRecipeCommand(recipe.Id, request, authorId.ToString(), false), CancellationToken.None);

        Assert.IsType<RecipeDto>(result);
        Assert.Equal(cat2.Name, result.CategoryName);
    }

    [Fact]
    public async Task Update_Nutrition_maps_all_6_fields()
    {
        await using var db = new RecipeAcceptanceContext();
        var authorId = Guid.NewGuid();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "C1", Slug = "c1" };
        db.Categories.Add(cat1);
        var recipe = new Recipe { Id = Guid.NewGuid(), Title = "A", Slug = "a", Difficulty = RecipeDifficulty.Easy, CategoryId = cat1.Id, AuthorId = authorId, Status = RecipeStatus.Draft };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var request = new UpdateRecipeRequest { Title = "A", CategoryId = cat1.Id, RowVersion = recipe.RowVersion.ToString(), Nutrition = new RecipeNutritionDto { Calories = 10, Protein = 20, Carbs = 30, Fat = 40, Fiber = 50, Sodium = 60 } };
        var handler = new CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe.UpdateRecipeCommandHandler(db);
        var result = await handler.Handle(new CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe.UpdateRecipeCommand(recipe.Id, request, authorId.ToString(), false), CancellationToken.None);

        Assert.NotNull(result.Nutrition);
        Assert.Equal(10, result.Nutrition.Calories);
        Assert.Equal(20, result.Nutrition.Protein);
        Assert.Equal(30, result.Nutrition.Carbs);
        Assert.Equal(40, result.Nutrition.Fat);
        Assert.Equal(50, result.Nutrition.Fiber);
        Assert.Equal(60, result.Nutrition.Sodium);
    }
    [Fact]
    public void Draft_with_valid_steps_and_ingredients_can_be_published_and_sets_publishedat()
    {
        var recipe = new Recipe { Status = RecipeStatus.Draft };
        recipe.Steps.Add(RecipeStep.Create(Guid.NewGuid(), 1, "Nấu"));
        recipe.Ingredients.Add(RecipeIngredient.Create(Guid.NewGuid(), "Thịt", 1, "kg", null, 0));

        Assert.Null(recipe.PublishedAt);
        recipe.Publish();

        Assert.Equal(RecipeStatus.Published, recipe.Status);
        Assert.NotNull(recipe.PublishedAt);
    }

    [Fact]
    public void Draft_to_unpublish_keeps_draft()
    {
        var recipe = new Recipe { Status = RecipeStatus.Draft };
        recipe.Unpublish();
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Published_to_unpublish_becomes_draft()
    {
        var recipe = new Recipe { Status = RecipeStatus.Published };
        recipe.Unpublish();
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }
}
