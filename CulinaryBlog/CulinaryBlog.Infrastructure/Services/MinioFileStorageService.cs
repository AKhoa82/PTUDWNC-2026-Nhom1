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

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;
    private bool _bucketInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MinioFileStorageService(
        IMinioClient minioClient,
        IOptions<MinioOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _minioClient = minioClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Đảm bảo Bucket tồn tại và cấu hình policy public-read (anonymous download).
    /// </summary>
    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_bucketInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketInitialized) return;

            var bucketName = _options.BucketName;
            var beArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool found = await _minioClient.BucketExistsAsync(beArgs, cancellationToken);

            if (!found)
            {
                _logger.LogInformation("Bucket '{BucketName}' chưa tồn tại. Đang tiến hành tạo mới...", bucketName);
                var mbArgs = new MakeBucketArgs().WithBucket(bucketName);
                await _minioClient.MakeBucketAsync(mbArgs, cancellationToken);
                _logger.LogInformation("Đã tạo bucket '{BucketName}' thành công.", bucketName);
            }

            // Thiết lập policy public-read cho phép người dùng đọc ảnh trực tiếp
            await EnsurePublicReadPolicyAsync(bucketName, cancellationToken);

            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra hoặc tạo bucket '{BucketName}' trên MinIO.", _options.BucketName);
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsurePublicReadPolicyAsync(string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            var policy = new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Sid = "PublicReadGetObject",
                        Effect = "Allow",
                        Principal = "*",
                        Action = new[] { "s3:GetObject" },
                        Resource = new[] { $"arn:aws:s3:::{bucketName}/*" }
                    }
                }
            };

            var policyJson = JsonSerializer.Serialize(policy);
            var spArgs = new SetPolicyArgs()
                .WithBucket(bucketName)
                .WithPolicy(policyJson);

            await _minioClient.SetPolicyAsync(spArgs, cancellationToken);
            _logger.LogInformation("Đã thiết lập public-read policy cho bucket '{BucketName}'.", bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể thiết lập public-read policy cho bucket '{BucketName}'. Client vẫn có thể truy cập nếu bucket đã được config policy trước.", bucketName);
        }
    }

    /// <summary>
    /// FR-FILE-001: Upload file lên MinIO
    /// </summary>
    public async Task<string> UploadAsync(IFormFile file, string folder = "uploads", CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra validation theo SRS: kích thước <= 5MB, mime-type và magic bytes
        await FileValidationHelper.ValidateImageFileAsync(file, cancellationToken);

        // 2. Chuẩn bị thông tin lưu trữ
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = file.OpenReadStream();

        return await UploadInternalAsync(stream, file.Length, file.ContentType, extension, folder, cancellationToken);
    }

    /// <summary>
    /// Upload file từ Stream lên MinIO
    /// </summary>
    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, string folder = "uploads", CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var length = stream.CanSeek ? stream.Length : -1;

        return await UploadInternalAsync(stream, length, contentType, extension, folder, cancellationToken);
    }

    private async Task<string> UploadInternalAsync(
        Stream stream,
        long length,
        string contentType,
        string extension,
        string folder,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        // 3. Tạo unique filename: {folder}/{Guid.NewGuid()}{ext} để ngăn path traversal
        var sanitizedFolder = string.IsNullOrWhiteSpace(folder)
            ? "uploads"
            : folder.Trim('/', '\\').Replace('\\', '/');

        var objectName = $"{sanitizedFolder}/{Guid.NewGuid():N}{extension}";

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);
        _logger.LogInformation("Đã upload file lên MinIO: {ObjectName} (Bucket: {BucketName}, Type: {ContentType})",
            objectName, _options.BucketName, contentType);

        // 4. Trả về public URL
        return BuildPublicUrl(objectName);
    }

    /// <summary>
    /// FR-FILE-002: Xóa file khỏi MinIO (Idempotent)
    /// </summary>
    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return;

        var objectName = ExtractObjectName(fileUrl);
        if (string.IsNullOrWhiteSpace(objectName))
        {
            _logger.LogWarning("Không thể trích xuất object name từ fileUrl: {FileUrl}", fileUrl);
            return;
        }

        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
            _logger.LogInformation("Đã xóa file khỏi MinIO: {ObjectName} (Bucket: {BucketName})", objectName, _options.BucketName);
        }
        catch (MinioException ex) when (ex is ObjectNotFoundException || ex is BucketNotFoundException)
        {
            // Idempotent: Nếu object không tồn tại, không ném ngoại lệ
            _logger.LogWarning("File không tồn tại trên MinIO khi thực hiện xóa: {ObjectName}", objectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa file '{ObjectName}' khỏi MinIO.", objectName);
            throw;
        }
    }

    /// <summary>
    /// Kiểm tra file có tồn tại trên MinIO hay không
    /// </summary>
    public async Task<bool> ExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return false;

        var objectName = ExtractObjectName(fileUrl);
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var statArgs = new StatObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName);

            var stat = await _minioClient.StatObjectAsync(statArgs, cancellationToken);
            return stat != null;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi kiểm tra sự tồn tại của file '{ObjectName}'.", objectName);
            return false;
        }
    }

    private string BuildPublicUrl(string objectName)
    {
        var baseUrl = _options.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var scheme = _options.UseSsl ? "https" : "http";
            baseUrl = $"{scheme}://{_options.Endpoint}";
        }

        baseUrl = baseUrl.TrimEnd('/');
        return $"{baseUrl}/{_options.BucketName}/{objectName}";
    }

    /// <summary>
    /// Trích xuất object name từ URL hoặc đường dẫn file
    /// Ví dụ:
    /// "http://localhost:9000/culinaryblog/recipes/abc.jpg" -> "recipes/abc.jpg"
    /// "culinaryblog/recipes/abc.jpg" -> "recipes/abc.jpg"
    /// "recipes/abc.jpg" -> "recipes/abc.jpg"
    /// </summary>
    private string ExtractObjectName(string fileUrl)
    {
        var rawUrl = fileUrl.Trim();

        // Nếu là full URL (bắt đầu bằng http:// hoặc https://)
        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            rawUrl = uri.AbsolutePath.TrimStart('/');
        }

        // Bỏ query string nếu có
        var queryIndex = rawUrl.IndexOf('?');
        if (queryIndex >= 0)
        {
            rawUrl = rawUrl.Substring(0, queryIndex);
        }

        var bucketPrefix = _options.BucketName + "/";
        if (rawUrl.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            rawUrl = rawUrl.Substring(bucketPrefix.Length);
        }

        return rawUrl.TrimStart('/');
    }
}
