using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class ImageResizeJob(ApplicationDbContext db, IFileStorageService storage, ILogger<ImageResizeJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(Guid imageId, CancellationToken ct)
    {
        // 1. Tìm ảnh để lấy khóa recipe và stored file.
        var metadata = await db.RecipeImages
            .AsNoTracking()
            .Where(x => x.Id == imageId)
            .Select(x => new
            {
                x.RecipeId,
                x.StoredFileId
            })
            .SingleOrDefaultAsync(ct);

        if (metadata?.StoredFileId is not Guid storedFileId)
        {
            logger.LogInformation("Image {ImageId} no longer requires processing", imageId);
            return;
        }

        // 2. Khóa bản ghi theo cùng thứ tự với upload.
        // Tránh xung đột với thao tác xóa ảnh.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Recipes\" WHERE \"Id\" = {metadata.RecipeId} FOR UPDATE",
            ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"StoredFiles\" WHERE \"Id\" = {storedFileId} FOR UPDATE",
            ct);

        // 3. Kiểm tra lại trạng thái sau khi đã khóa.
        var image = await db.RecipeImages
            .Include(x => x.StoredFile)
            .SingleOrDefaultAsync(x => x.Id == imageId, ct);

        if (image is null)
            return;

        var recipe = await db.Recipes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == image.RecipeId, ct);

        var storedFile = image.StoredFile;

        if (recipe is null || image.StoredFileId != storedFileId || storedFile is null || storedFile.Status != StoredFileStatus.Active || storedFile.DeletedAt is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(storedFile.BucketName) || string.IsNullOrWhiteSpace(storedFile.ObjectKey))
        {
            logger.LogWarning("Image {ImageId} has no valid storage reference", imageId);
            return;
        }

        // Job đã hoàn tất thì không tạo ảnh lại.
        if (image.MediumUrl is not null && image.ThumbnailUrl is not null)
        {
            return;
        }

        // 4. Tạo đường dẫn cố định.
        var original = new StorageObjectReference(storedFile.BucketName, storedFile.ObjectKey);

        if (!RecipeImageVariantKeys.TryCreate(original, out var mediumReference, out var thumbnailReference))
        {
            throw new InvalidOperationException("Invalid recipe image object key.");
        }

        var mediumUrl = storage.GetPublicUrl(mediumReference);
        var thumbnailUrl = storage.GetPublicUrl(thumbnailReference);

        if (mediumUrl.Length > 500 || thumbnailUrl.Length > 500)
        {
            throw new InvalidOperationException("Generated image URL exceeds 500 characters.");
        }

        // 5. Tải ảnh gốc từ MinIO.
        var originalBytes = await storage.DownloadBytesAsync(original, ct);

        // Kiểm tra kích thước pixel trước khi giải mã.
        var info = new MagickImageInfo(originalBytes);

        if (info.Width == 0 || info.Height == 0 || (ulong)info.Width * info.Height > 40_000_000)
        {
            throw new InvalidOperationException("Unsupported image dimensions.");
        }

        using var source = new MagickImage(originalBytes);

        // Xử lý hướng ảnh theo EXIF.
        source.AutoOrient();

        ct.ThrowIfCancellationRequested();

        // 6. Tạo medium 800x600.
        using var mediumStream = CreateVariant(source, 800, 600);

        await storage.UploadStreamAsync(mediumStream, "medium.webp", "image/webp", mediumReference, ct);

        ct.ThrowIfCancellationRequested();

        // 7. Tạo thumbnail 300x300.
        using var thumbnailStream = CreateVariant(source, 300, 300);

        await storage.UploadStreamAsync(thumbnailStream, "thumbnail.webp", "image/webp", thumbnailReference, ct);

        // 8. Chỉ cập nhật database sau khi
        // cả hai ảnh đã upload thành công.
        image.MediumUrl = mediumUrl;
        image.ThumbnailUrl = thumbnailUrl;

        db.RecipeCacheInvalidations.Add(
            new RecipeCacheInvalidation
            {
                RecipeSlug = recipe.Slug
            });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("FR-JOB-002 completed for image {ImageId}", imageId);
    }

    private static MemoryStream CreateVariant(MagickImage source, uint width, uint height)
    {
        using var resized = new MagickImage(source);

        // Resize theo tỷ lệ, không kéo méo ảnh.
        resized.Resize(new MagickGeometry(width, height)
        {
            FillArea = true
        });

        // Cắt chính giữa về kích thước chính xác.
        resized.Extent(width, height, Gravity.Center);
        resized.ResetPage();

        // Xóa metadata không cần thiết.
        resized.Strip();

        resized.Format = MagickFormat.WebP;
        resized.Quality = 82;

        var output = new MemoryStream();
        resized.Write(output);
        output.Position = 0;

        return output;
    }
}