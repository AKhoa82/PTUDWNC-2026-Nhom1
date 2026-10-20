using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CulinaryBlog.Infrastructure.Services;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class DeleteStoredFileJob(
    ApplicationDbContext db,
    IFileStorageService storage,
    ILogger<DeleteStoredFileJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(Guid storedFileId, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StoredFiles\" WHERE \"Id\" = {storedFileId} FOR UPDATE", ct);
        var file = await db.StoredFiles.SingleOrDefaultAsync(item => item.Id == storedFileId, ct);
        if (file is null || file.Status != StoredFileStatus.DeletePending || file.DeletedAt != null) return;
        if (await db.RecipeImages.AnyAsync(image => image.StoredFileId == storedFileId, ct))
            throw new InvalidOperationException("Cannot delete a file still attached to a recipe image.");
        if (file.BucketName is null || file.ObjectKey is null)
            throw new InvalidOperationException("Storage reference is unverified; run the backfill tool before retrying.");
        try
        {
            var original = new StorageObjectReference(file.BucketName, file.ObjectKey);

            // Xóa cả hai biến thể nếu đây là ảnh recipe.
            if (RecipeImageVariantKeys.TryCreate(original, out var medium, out var thumbnail))
            {
                await storage.DeleteAsync(medium, ct);
                await storage.DeleteAsync(thumbnail, ct);
            }

            // Xóa ảnh gốc sau cùng.
            await storage.DeleteAsync(original, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MinIO deletion failed for stored file {StoredFileId}", storedFileId);

            throw;
        }
        file.DeletedAt = DateTime.UtcNow;
        file.Status = StoredFileStatus.Deleted;
        file.DeletionRequestedAt ??= file.DeletedAt;
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        logger.LogInformation("Deleted stored file {StoredFileId} at {Timestamp}", storedFileId, file.DeletedAt);
    }
}
