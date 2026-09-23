using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Files;

public sealed class FileManagementService(
    IApplicationDbContext db,
    IFileStorageService storage,
    ILogger<FileManagementService> logger)
{
    public async Task<UploadFileResultDto> UploadAsync(IFormFile file, string? folder, Guid userId, CancellationToken ct)
    {
        await RequireUserAsync(userId, ct);
        var safeFolder = FileValidationHelper.ValidateFolder(folder);
        if (safeFolder.Length > 100) throw new ArgumentException("Folder không được vượt quá 100 ký tự.");
        var url = await storage.UploadAsync(file, $"users/{userId:N}/{safeFolder}", ct);
        try
        {
            if (url.Length > 512)
                throw new InvalidOperationException("The configured public file URL is too long.");
            db.StoredFiles.Add(new StoredFile { OwnerId = userId, Url = url, SizeBytes = file.Length });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The object and database are not one transaction. Compensate failed registration.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await storage.DeleteAsync(url, cleanup.Token); }
            catch (Exception ex) { logger.LogError(ex, "Orphan file cleanup failed for {FileUrl}", url); }
            throw;
        }
        logger.LogInformation("File uploaded by {UserId} at {Timestamp}: {FileUrl}", userId, DateTime.UtcNow, url);
        return new UploadFileResultDto(url, file.FileName, file.ContentType, file.Length);
    }

    public async Task DeleteAsync(string fileUrl, Guid userId, CancellationToken ct)
    {
        var file = await RequireOwnerAsync(fileUrl, userId, ct);
        if (file.DeletedAt != null) return;
        // Keep the row active on storage failure so the caller can retry safely.
        await storage.DeleteAsync(file.Url, ct);
        file.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("File deleted by {UserId} at {Timestamp}: {FileUrl}", userId, file.DeletedAt, file.Url);
    }

    public async Task<bool> ExistsAsync(string fileUrl, Guid userId, CancellationToken ct)
    {
        var file = await RequireOwnerAsync(fileUrl, userId, ct);
        return file.DeletedAt == null && await storage.ExistsAsync(file.Url, ct);
    }

    private async Task<StoredFile> RequireOwnerAsync(string url, Guid userId, CancellationToken ct)
    {
        await RequireUserAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(url) || url.Length > 512)
            throw new ArgumentException("URL tệp tin không hợp lệ.");
        var file = await db.StoredFiles.SingleOrDefaultAsync(f => f.Url == url, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tệp tin đã đăng ký.");
        if (file.OwnerId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền thao tác tệp tin này.");
        return file;
    }

    private async Task RequireUserAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty || !await db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new UnauthorizedAccessException("Tài khoản không hợp lệ.");
    }
}
