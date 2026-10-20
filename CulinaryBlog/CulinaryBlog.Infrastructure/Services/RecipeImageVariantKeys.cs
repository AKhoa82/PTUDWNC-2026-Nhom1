using CulinaryBlog.Application.Contracts.Infrastructure;

namespace CulinaryBlog.Infrastructure.Services;

public static class RecipeImageVariantKeys
{
    public static bool TryCreate(
        StorageObjectReference original,
        out StorageObjectReference medium,
        out StorageObjectReference thumbnail)
    {
        medium = default!;
        thumbnail = default!;

        var key = original.ObjectKey;

        if (!key.StartsWith("recipes/", StringComparison.Ordinal))
            return false;

        var slash = key.LastIndexOf('/');
        var dot = key.LastIndexOf('.');

        if (slash < 0 || dot <= slash)
            return false;

        var stem = key[..dot];

        medium = new StorageObjectReference(
            original.BucketName,
            $"{stem}-medium.webp");

        thumbnail = new StorageObjectReference(
            original.BucketName,
            $"{stem}-thumbnail.webp");

        return true;
    }
}