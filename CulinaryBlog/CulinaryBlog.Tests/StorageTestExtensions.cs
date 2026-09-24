using CulinaryBlog.Application.Contracts.Infrastructure;

namespace CulinaryBlog.Tests;

// Only test-owned URLs produced by fixtures may use this convenience adapter.
internal static class StorageTestExtensions
{
    public static StorageObjectReference ReferenceFromFixtureUrl(string url)
    {
        var parts = new Uri(url).AbsolutePath.TrimStart('/').Split('/', 2);
        return new(parts[0], Uri.UnescapeDataString(parts[1]));
    }
    public static Task<bool> ExistsAsync(this IFileStorageService storage, string fixtureUrl) =>
        storage.ExistsAsync(ReferenceFromFixtureUrl(fixtureUrl));
    public static Task DeleteAsync(this IFileStorageService storage, string fixtureUrl) =>
        storage.DeleteAsync(ReferenceFromFixtureUrl(fixtureUrl));
}
