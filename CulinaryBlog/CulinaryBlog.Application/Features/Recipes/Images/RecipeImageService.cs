using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Recipes.Images;

public sealed class RecipeImageService(
    IApplicationDbContext db,
    IFileStorageService storage,
    IFileDeletionQueue deletionQueue,
    IRecipeImageTransactionFactory transactions,
    IFileLifecycleSessionFactory sessions,
    ILogger<RecipeImageService> logger)
{
    public async Task<RecipeImageDto> UploadAsync(
        Guid recipeId, IFormFile file, string? altText, bool requestedPrimary,
        string userId, bool isAdmin, CancellationToken ct)
    {
        ValidateAltText(altText);
        await FileValidationHelper.ValidateImageFileAsync(file, ct);
        var reference = storage.CreateReference($"recipes/{recipeId:N}", file.FileName);
        var url = storage.GetPublicUrl(reference);
        if (url.Length > 500) throw new FileOperationUnavailableException("Public image URL exceeds 500 characters.");
        var intent = new StoredFile
        {
            Url = url, SizeBytes = file.Length, BucketName = reference.BucketName,
            ObjectKey = reference.ObjectKey, Status = StoredFileStatus.PendingUpload,
            UploadExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await using (var reservation = sessions.Create())
        {
            await GetAuthorizedRecipeAsync(reservation.Db, recipeId, userId, isAdmin, ct);
            intent.OwnerId = Guid.Parse(userId);
            reservation.Db.StoredFiles.Add(intent);
            try { await reservation.Db.SaveChangesAsync(ct); }
            catch (Exception ex) { throw new FileOperationUnavailableException("Could not confirm upload reservation. No upload was started.", ex); }
        }
        var imageId = Guid.NewGuid();
        RecipeImageDto result;
        try { result = await UploadAndCommitAsync(); }
        catch (Exception uploadError)
        {
            logger.LogWarning(uploadError, "Verifying upload outcome for stored file {StoredFileId}", intent.Id);
            // UploadAndCommitAsync has disposed its context/transaction before recovery starts.
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await using var recovery = sessions.Create();
                await using var recoveryTx = await recovery.BeginAsync(null, intent.Id, recoveryTimeout.Token);
                var persistedImage = await recovery.Db.RecipeImages.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.StoredFileId == intent.Id, recoveryTimeout.Token);
                var persistedFile = await recovery.Db.StoredFiles.SingleAsync(x => x.Id == intent.Id, recoveryTimeout.Token);
                if (persistedImage != null && persistedFile.Status == StoredFileStatus.Active)
                {
                    result = Map(persistedImage);
                }
                else
                {
                    if (persistedImage != null) throw new InvalidOperationException("Inconsistent file state; preserving object.");
                    if (persistedFile.Status == StoredFileStatus.PendingUpload)
                    {
                        persistedFile.Status = StoredFileStatus.DeletePending;
                        persistedFile.DeletionRequestedAt = DateTime.UtcNow;
                        await recovery.Db.SaveChangesAsync(recoveryTimeout.Token);
                    }
                    await recoveryTx.CommitAsync(recoveryTimeout.Token);
                    if (uploadError is RecipeImageValidationException or UnauthorizedAccessException or KeyNotFoundException or ArgumentException)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(uploadError).Throw();
                    throw new FileOperationUnavailableException("Upload did not complete; cleanup is scheduled.", uploadError);
                }
            }
            catch (FileOperationUnavailableException) { throw; }
            catch (Exception ex) when (ReferenceEquals(ex, uploadError)) { throw; }
            catch (Exception verificationError)
            {
                logger.LogError(verificationError, "Uncertain upload outcome for {StoredFileId}; object preserved", intent.Id);
                throw new FileOperationUnavailableException("Cannot confirm upload outcome. Reload the gallery before retrying.", verificationError);
            }
        }
        logger.LogInformation("User {UserId} uploaded recipe image {ImageId} for {RecipeId}", userId, result.ImageId, recipeId);
        return result;

        async Task<RecipeImageDto> UploadAndCommitAsync()
        {
            await using var work = sessions.Create();
            await using var transaction = await work.BeginAsync(recipeId, intent.Id, ct);
            var recipe = await GetAuthorizedRecipeAsync(work.Db, recipeId, userId, isAdmin, ct);
            var storedFile = await work.Db.StoredFiles.SingleAsync(x => x.Id == intent.Id, ct);
            if (storedFile.Status != StoredFileStatus.PendingUpload)
                throw new InvalidOperationException("Upload reservation is no longer pending.");
            var isPrimary = recipe.Images.Count == 0 || requestedPrimary;
            var lastIndex = recipe.Images.Count == 0 ? -1 : recipe.Images.Max(x => x.OrderIndex);
            if (lastIndex == int.MaxValue) throw new RecipeImageValidationException("Thứ tự ảnh đã đạt giới hạn.");
            await storage.UploadAsync(file, reference, ct);
            if (isPrimary)
            {
                foreach (var current in recipe.Images.Where(x => x.IsPrimary)) current.IsPrimary = false;
                await work.Db.SaveChangesAsync(ct);
                recipe.ImageUrl = url;
            }
            var image = new RecipeImage
            {
                Id = imageId, RecipeId = recipeId, StoredFileId = intent.Id, OriginalUrl = url,
                AltText = NormalizeAltText(altText), IsPrimary = isPrimary, OrderIndex = lastIndex + 1
            };
            recipe.Images.Add(image);
            work.Db.RecipeImages.Add(image);
            storedFile.Status = StoredFileStatus.Active;
            storedFile.UploadExpiresAt = null;
            recipe.UpdatedAt = DateTime.UtcNow;
            work.Db.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
            await work.Db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Map(image);
        }
    }

    public async Task<RecipeImageDto> UpdateAsync(
        Guid recipeId, Guid imageId, UpdateRecipeImageRequest request,
        string userId, bool isAdmin, CancellationToken ct)
    {
        if (!request.AltTextSpecified && request.IsPrimary is null && request.OrderIndex is null)
            throw new RecipeImageValidationException("Phải cung cấp ít nhất một trường cần cập nhật.");
        ValidateAltText(request.AltText);
        if (request.OrderIndex < 0)
            throw new RecipeImageValidationException("orderIndex phải lớn hơn hoặc bằng 0.");

        await using var transaction = await transactions.BeginAsync(recipeId, ct);
        var recipe = await GetAuthorizedRecipeAsync(db, recipeId, userId, isAdmin, ct);
        var image = recipe.Images.SingleOrDefault(item => item.Id == imageId)
            ?? throw new KeyNotFoundException("Không tìm thấy ảnh trong công thức này.");

        if (request.IsPrimary == false && image.IsPrimary)
            throw new RecipeImageValidationException("Hãy đặt một ảnh khác làm ảnh chính thay vì bỏ ảnh chính hiện tại.");
        if (request.AltTextSpecified) image.AltText = NormalizeAltText(request.AltText);
        if (request.OrderIndex is not null) image.OrderIndex = request.OrderIndex.Value;
        if (request.IsPrimary == true && !image.IsPrimary)
        {
            foreach (var current in recipe.Images.Where(current => current.IsPrimary))
                current.IsPrimary = false;
            await db.SaveChangesAsync(ct);
            image.IsPrimary = true;
            recipe.ImageUrl = image.OriginalUrl;
        }

        recipe.UpdatedAt = DateTime.UtcNow;
        db.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("User {UserId} updated recipe image {ImageId} for {RecipeId} at {Timestamp}",
            userId, imageId, recipeId, DateTime.UtcNow);
        return Map(image);
    }

    public async Task DeleteAsync(
        Guid recipeId, Guid imageId, string userId, bool isAdmin, CancellationToken ct)
    {
        await using var transaction = await transactions.BeginAsync(recipeId, ct);
        var recipe = await GetAuthorizedRecipeAsync(db, recipeId, userId, isAdmin, ct);
        var image = recipe.Images.SingleOrDefault(item => item.Id == imageId)
            ?? throw new KeyNotFoundException("Không tìm thấy ảnh trong công thức này.");
        var storedFile = image.StoredFile;
        var wasPrimary = image.IsPrimary;
        recipe.Images.Remove(image);
        db.RecipeImages.Remove(image);
        // Delete the old primary before promoting its replacement; both saves are atomic.
        await db.SaveChangesAsync(ct);

        if (wasPrimary)
        {
            var replacement = recipe.Images.OrderBy(item => item.OrderIndex).ThenBy(item => item.Id).FirstOrDefault();
            if (replacement is not null) replacement.IsPrimary = true;
            recipe.ImageUrl = replacement?.OriginalUrl;
        }
        if (storedFile is not null && storedFile.DeletedAt is null)
        {
            storedFile.Status = StoredFileStatus.DeletePending;
            storedFile.DeletionRequestedAt ??= DateTime.UtcNow;
        }

        recipe.UpdatedAt = DateTime.UtcNow;
        db.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (storedFile is not null && storedFile.DeletedAt is null)
        {
            try { deletionQueue.Enqueue(storedFile.Id); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not enqueue deletion for stored file {StoredFileId}; reconciliation will retry", storedFile.Id);
            }
        }
        logger.LogInformation("User {UserId} deleted recipe image {ImageId} for {RecipeId} at {Timestamp}",
            userId, imageId, recipeId, DateTime.UtcNow);
    }

    private static async Task<Recipe> GetAuthorizedRecipeAsync(IApplicationDbContext db, Guid recipeId, string userId, bool isAdmin, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty ||
            !await db.Users.AnyAsync(user => user.Id == actorId, ct))
            throw new UnauthorizedAccessException("Tài khoản không hợp lệ.");
        var recipe = await db.Recipes.Include(item => item.Images).ThenInclude(image => image.StoredFile)
            .SingleOrDefaultAsync(item => item.Id == recipeId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy công thức.");
        if (!isAdmin && (!Guid.TryParse(recipe.AuthorId, out var ownerId) || ownerId != actorId))
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý ảnh của công thức này.");
        return recipe;
    }

    private static void ValidateAltText(string? value)
    {
        if (value?.Trim().Length > 200)
            throw new RecipeImageValidationException("altText không được vượt quá 200 ký tự.");
    }

    private static string? NormalizeAltText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecipeImageDto Map(RecipeImage image) => new(
        image.Id, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl,
        image.AltText, image.IsPrimary, image.OrderIndex);
}
