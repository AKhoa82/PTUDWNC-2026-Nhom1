using Microsoft.AspNetCore.Http;

namespace CulinaryBlog.Application.Contracts.Infrastructure;

public sealed record StorageObjectReference(string BucketName, string ObjectKey);

// References originate in application code/database, never HTTP input.
public interface IFileStorageService
{
    StorageObjectReference CreateReference(string folder, string fileName);
    string GetPublicUrl(StorageObjectReference reference);
    Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken ct = default);
    Task UploadStreamAsync(Stream stream, string fileName, string contentType, StorageObjectReference reference, CancellationToken ct = default);
    Task DeleteAsync(StorageObjectReference reference, CancellationToken ct = default);
    Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken ct = default);
}
