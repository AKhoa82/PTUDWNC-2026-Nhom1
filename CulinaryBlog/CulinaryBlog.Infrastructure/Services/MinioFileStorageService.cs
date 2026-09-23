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

    public async Task<string> UploadAsync(IFormFile file, string folder = "uploads", CancellationToken cancellationToken = default)
    {
        await FileValidationHelper.ValidateImageFileAsync(file, cancellationToken);
        var safeFolder = FileValidationHelper.ValidateFolder(folder);
        using var stream = file.OpenReadStream();
        return await UploadValidatedAsync(stream, file.Length, file.FileName, file.ContentType, safeFolder, cancellationToken);
    }

    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, string folder = "uploads", CancellationToken cancellationToken = default)
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
        return await UploadAsync(file, folder, cancellationToken);
    }

    private async Task<string> UploadValidatedAsync(Stream stream, long length, string fileName, string contentType, string folder, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        var objectName = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(_options.BucketName)
            .WithObject(objectName).WithStreamData(stream).WithObjectSize(length).WithContentType(contentType), ct);
        return BuildPublicUrl(objectName);
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var objectName = ExtractObjectName(fileUrl);
        try
        {
            // Do not create buckets or change their policy on DELETE.
            await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.BucketName)
                .WithObject(objectName), cancellationToken);
        }
        catch (MinioException ex) when (ex is ObjectNotFoundException or BucketNotFoundException)
        {
            // Already absent is success. Connectivity/permission failures must reach the caller.
        }
    }

    public async Task<bool> ExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var objectName = ExtractObjectName(fileUrl);
        try
        {
            await client.StatObjectAsync(new StatObjectArgs().WithBucket(_options.BucketName)
                .WithObject(objectName), cancellationToken);
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

    private string PublicPrefix =>
        (string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{(_options.UseSsl ? "https" : "http")}://{_options.Endpoint}" : _options.PublicBaseUrl).TrimEnd('/')
        + "/" + _options.BucketName + "/";

    private string BuildPublicUrl(string objectName) =>
        PublicPrefix + string.Join('/', objectName.Split('/').Select(Uri.EscapeDataString));

    private string ExtractObjectName(string fileUrl)
    {
        // Only canonical URLs produced by this storage provider are accepted.
        if (string.IsNullOrWhiteSpace(fileUrl) || !fileUrl.StartsWith(PublicPrefix, StringComparison.Ordinal) ||
            !Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("URL không thuộc kho lưu trữ được cấu hình.");
        var key = Uri.UnescapeDataString(fileUrl[PublicPrefix.Length..]);
        var segments = key.Split('/');
        if (segments.Any(s => string.IsNullOrEmpty(s) || s is "." or ".." ||
                s.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')) ||
            BuildPublicUrl(key) != fileUrl)
            throw new ArgumentException("Object key không hợp lệ.");
        return key;
    }

    public void Dispose() => _initLock.Dispose();
}
