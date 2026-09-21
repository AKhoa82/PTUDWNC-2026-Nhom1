using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Minio.Exceptions;

namespace CulinaryBlog.API.Endpoints;

public static class FileEndpoints
{
    public static RouteGroupBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files")
            .WithTags("Files")
            .AllowAnonymous();

        // FR-FILE-001: Upload File lên MinIO
        group.MapPost("/upload", async (
            IFormFile file,
            [FromQuery] string? folder,
            IFileStorageService fileStorageService,
            CancellationToken cancellationToken) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Vui lòng chọn tệp tin cần tải lên." });
            }

            try
            {
                var targetFolder = string.IsNullOrWhiteSpace(folder) ? "uploads" : folder.Trim();
                var publicUrl = await fileStorageService.UploadAsync(file, targetFolder, cancellationToken);

                var response = new UploadFileResultDto(
                    Url: publicUrl,
                    FileName: file.FileName,
                    ContentType: file.ContentType,
                    SizeBytes: file.Length
                );

                return Results.Created(publicUrl, response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (MinioException ex)
            {
                return Results.Problem(
                    detail: $"Dịch vụ lưu trữ MinIO không khả dụng: {ex.Message}",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "MinIO Service Unavailable");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Đã xảy ra lỗi khi tải lên tệp tin.");
            }
        })
        .WithName("UploadFileV1")
        .WithSummary("FR-FILE-001 – Upload file ảnh lên MinIO S3 (tối đa 5MB, JPEG/PNG/WebP/AVIF, Magic Bytes validation)")
        .WithDescription("Tải lên tệp tin ảnh đa phương tiện lên hệ thống lưu trữ MinIO. File được kiểm tra kích thước (tối đa 5MB), định dạng MIME và magic bytes thực tế để đảm bảo an toàn.")
        .DisableAntiforgery();

        // FR-FILE-002: Xóa File khỏi MinIO
        group.MapDelete("/", async (
            [FromQuery] string fileUrl,
            IFileStorageService fileStorageService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return Results.BadRequest(new { message = "Tham số 'fileUrl' là bắt buộc." });
            }

            try
            {
                await fileStorageService.DeleteAsync(fileUrl, cancellationToken);
                return Results.NoContent();
            }
            catch (MinioException ex)
            {
                return Results.Problem(
                    detail: $"Dịch vụ MinIO gặp sự cố khi xóa file: {ex.Message}",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "MinIO Service Unavailable");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Đã xảy ra lỗi khi xóa tệp tin.");
            }
        })
        .WithName("DeleteFileV1")
        .WithSummary("FR-FILE-002 – Xóa file khỏi MinIO (Idempotent)")
        .WithDescription("Xóa đối tượng tệp tin lưu trên MinIO dựa theo URL công khai. Nếu file không tồn tại, phương thức xử lý an toàn (idempotent) và vẫn trả về HTTP 204 No Content.");

        // Endpoint kiểm tra sự tồn tại của file
        group.MapGet("/exists", async (
            [FromQuery] string fileUrl,
            IFileStorageService fileStorageService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return Results.BadRequest(new { message = "Tham số 'fileUrl' là bắt buộc." });
            }

            var exists = await fileStorageService.ExistsAsync(fileUrl, cancellationToken);
            return Results.Ok(new { fileUrl, exists });
        })
        .WithName("CheckFileExistsV1")
        .WithSummary("Kiểm tra file có tồn tại trên MinIO hay không");

        return group;
    }
}
