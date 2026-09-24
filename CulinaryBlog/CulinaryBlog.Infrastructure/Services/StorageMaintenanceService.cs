using System.Text.Json;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Services;

// Explicit operator command only; never mapped as a public endpoint.
public sealed class StorageMaintenanceService(ApplicationDbContext db, IMinioClient client,
    IOptions<MinioOptions> options, IFileStorageService storage)
{
    public StorageObjectReference? Resolve(string url)
    {
        var config = options.Value;
        var currentPrefix = storage.GetPublicUrl(new(config.BucketName, "probe"))[..^5];
        var locations = config.LegacyLocations.Append(new LegacyStorageLocation
        { PublicPrefix = currentPrefix, BucketName = config.BucketName });
        foreach (var location in locations.OrderByDescending(x => x.PublicPrefix.Length))
        {
            if (!Uri.TryCreate(location.PublicPrefix, UriKind.Absolute, out var prefixUri) ||
                prefixUri.Scheme is not ("http" or "https") || !location.PublicPrefix.EndsWith('/') ||
                !string.IsNullOrEmpty(prefixUri.Query) || !string.IsNullOrEmpty(prefixUri.Fragment) || !string.IsNullOrEmpty(prefixUri.UserInfo) ||
                !url.StartsWith(location.PublicPrefix, StringComparison.Ordinal)) continue;
            var escapedKey = url[location.PublicPrefix.Length..];
            var key = Uri.UnescapeDataString(escapedKey);
            var reference = new StorageObjectReference(location.BucketName, key);
            try
            {
                storage.GetPublicUrl(reference); // validates bucket/key without contacting storage
                if (string.Join('/', key.Split('/').Select(Uri.EscapeDataString)) == escapedKey) return reference;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public async Task BackfillAsync(bool apply, TextWriter report, CancellationToken ct)
    {
        var ids = await db.StoredFiles.AsNoTracking().Where(x => x.BucketName == null || x.ObjectKey == null)
            .Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            var file = await db.StoredFiles.SingleAsync(x => x.Id == id, ct);
            var reference = Resolve(file.Url);
            var duplicate = reference != null && await db.StoredFiles.AnyAsync(x => x.Id != id &&
                x.BucketName == reference.BucketName && x.ObjectKey == reference.ObjectKey, ct);
            await report.WriteLineAsync(JsonSerializer.Serialize(new { file.Id, file.Url, reference,
                result = reference == null ? "unresolved" : duplicate ? "duplicate-reference" : apply ? "apply" : "candidate" }));
            if (apply && reference != null && !duplicate)
            {
                file.BucketName = reference.BucketName;
                file.ObjectKey = reference.ObjectKey;
                await db.SaveChangesAsync(ct);
            }
            db.ChangeTracker.Clear();
        }
        var owners = await db.Users.Select(x => x.Id.ToString()).ToListAsync(ct);
        await foreach (var recipe in db.Recipes.AsNoTracking().Select(x => new { x.Id, x.AuthorId }).AsAsyncEnumerable().WithCancellation(ct))
            if (!Guid.TryParse(recipe.AuthorId, out var author) || !owners.Contains(author.ToString()))
                await report.WriteLineAsync(JsonSerializer.Serialize(new { recipe.Id, recipe.AuthorId, result = "invalid-recipe-owner" }));
    }

    public async Task InventoryAsync(TextWriter report, CancellationToken ct)
    {
        var buckets = options.Value.LegacyLocations.Select(x => x.BucketName).Append(options.Value.BucketName).Distinct();
        foreach (var bucket in buckets)
        {
            storage.GetPublicUrl(new(bucket, "probe"));
            await foreach (var item in client.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithRecursive(true), ct))
            {
                if (!await db.StoredFiles.AnyAsync(x => x.BucketName == bucket && x.ObjectKey == item.Key, ct))
                    await report.WriteLineAsync(JsonSerializer.Serialize(new { bucket, objectKey = item.Key, item.Size,
                        result = "untracked-candidate-not-approved-for-deletion" }));
            }
        }
    }
}
