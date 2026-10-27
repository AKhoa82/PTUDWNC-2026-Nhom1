using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Services;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeImageVariantKeysTests
{
    [Fact]
    public void ValidRecipeImage_GeneratesExpectedKeys()
    {
        var original = new StorageObjectReference(
            "culinary-blog",
            "recipes/123/photo.jpg");

        var success = RecipeImageVariantKeys.TryCreate(
            original,
            out var medium,
            out var thumbnail);

        Assert.True(success);

        Assert.Equal(
            "recipes/123/photo-medium.webp",
            medium.ObjectKey);

        Assert.Equal(
            "recipes/123/photo-thumbnail.webp",
            thumbnail.ObjectKey);

        Assert.Equal("culinary-blog", medium.BucketName);
        Assert.Equal("culinary-blog", thumbnail.BucketName);
    }

    [Fact]
    public void RepeatedCalls_GenerateSameKeys()
    {
        var original = new StorageObjectReference(
            "culinary-blog",
            "recipes/123/photo.png");

        RecipeImageVariantKeys.TryCreate(
            original, out var medium1, out var thumbnail1);

        RecipeImageVariantKeys.TryCreate(
            original, out var medium2, out var thumbnail2);

        Assert.Equal(medium1, medium2);
        Assert.Equal(thumbnail1, thumbnail2);
    }

    [Fact]
    public void InvalidFolder_DoesNotGenerateVariants()
    {
        var original = new StorageObjectReference(
            "culinary-blog",
            "avatars/photo.jpg");

        var success = RecipeImageVariantKeys.TryCreate(
            original, out _, out _);

        Assert.False(success);
    }
}