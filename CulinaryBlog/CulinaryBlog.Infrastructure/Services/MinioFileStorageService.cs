using System.Text.Json;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class MinioFileStorageService(
    IMinioClient client, IOptions<MinioOptions> options,
    ILogger<MinioFileStorageService> logger) : IFileStorageService, IDisposable
{
    private readonly MinioOptions _options = options.Value;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    public StorageObjectReference CreateReference(string folder, string fileName) => new(
        _options.BucketName, $"{FileValidationHelper.ValidateFolder(folder)}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}");

    public string GetPublicUrl(StorageObjectReference reference)
    {
        ValidateReference(reference);
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{(_options.UseSsl ? "https" : "http")}://{_options.Endpoint}" : _options.PublicBaseUrl;
        return baseUrl.TrimEnd('/') + "/" + reference.BucketName + "/" + string.Join('/', reference.ObjectKey.Split('/').Select(Uri.EscapeDataString));
    }

    private static void ValidateReference(StorageObjectReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.BucketName) || reference.BucketName.Length > 63 ||
            reference.BucketName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '.'))
            throw new ArgumentException("Invalid bucket name.");
        if (string.IsNullOrWhiteSpace(reference.ObjectKey) || reference.ObjectKey.Length > 512 ||
            reference.ObjectKey.Split('/').Any(s => string.IsNullOrEmpty(s) || s is "." or ".." ||
                s.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')))
            throw new ArgumentException("Invalid object key.");
    }

    public async Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        await FileValidationHelper.ValidateImageFileAsync(file, cancellationToken);
        ValidateReference(reference);
        if (reference.BucketName != _options.BucketName) throw new ArgumentException("Uploads require the configured bucket.");
        using var stream = file.OpenReadStream();
        await UploadValidatedAsync(stream, file.Length, file.ContentType, reference, cancellationToken);
    }

    public async Task UploadStreamAsync(Stream stream, string fileName, string contentType, StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        // Bound buffering for non-seekable streams; apply the same validation as HTTP uploads.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > FileValidationHelper.MaxFileSizeBytes)
                throw new ArgumentException("Kích thước file vượt quá giới hạn 5MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        buffer.Position = 0;
        var file = new FormFile(buffer, 0, buffer.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(), ContentType = contentType
        };
        await UploadAsync(file, reference, cancellationToken);
    }

    private async Task UploadValidatedAsync(Stream stream, long length, string contentType, StorageObjectReference reference, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(reference.BucketName)
            .WithObject(reference.ObjectKey).WithStreamData(stream).WithObjectSize(length).WithContentType(contentType), ct);
    }

    public async Task DeleteAsync(StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        try
        {
            // Do not create buckets or change their policy on DELETE.
            await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(reference.BucketName)
                .WithObject(reference.ObjectKey), cancellationToken);
        }
        catch (MinioException ex) when (ex is ObjectNotFoundException or BucketNotFoundException)
        {
            // Already absent is success. Connectivity/permission failures must reach the caller.
        }
    }

    public async Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        try
        {
            await client.StatObjectAsync(new StatObjectArgs().WithBucket(reference.BucketName)
                .WithObject(reference.ObjectKey), cancellationToken);
            return true;
        }
        catch (MinioException ex) when (ex is ObjectNotFoundException or BucketNotFoundException)
        {
            return false;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.BucketName), ct))
            {
                try { await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.BucketName), ct); }
                catch (MinioException)
                {
                    // Another API instance may have created it concurrently.
                    if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.BucketName), ct)) throw;
                }
            }
            var policy = JsonSerializer.Serialize(new
            {
                Version = "2012-10-17",
                Statement = new[] { new {
                    Effect = "Allow", Principal = "*", Action = new[] { "s3:GetObject" },
                    Resource = new[] { $"arn:aws:s3:::{_options.BucketName}/*" }
                } }
            });
            await client.SetPolicyAsync(new SetPolicyArgs().WithBucket(_options.BucketName).WithPolicy(policy), ct);
            _initialized = true;
            logger.LogInformation("MinIO public image bucket {Bucket} is ready", _options.BucketName);
        }
        finally { _initLock.Release(); }
    }

    public void Dispose() => _initLock.Dispose();
}
