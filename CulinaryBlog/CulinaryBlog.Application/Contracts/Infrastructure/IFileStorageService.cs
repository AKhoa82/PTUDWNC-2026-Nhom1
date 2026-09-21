using Microsoft.AspNetCore.Http;

namespace CulinaryBlog.Application.Contracts.Infrastructure;

public interface IFileStorageService
{
    /// <summary>
    /// Upload file lên MinIO theo folder chỉ định.
    /// Tạo tên file độc nhất {folder}/{Guid.NewGuid()}{ext} để chống path traversal.
    /// Validate kích thước tối đa 5MB, MIME type (JPEG/PNG/WebP/AVIF) và magic bytes.
    /// </summary>
    /// <param name="file">File gửi lên từ request</param>
    /// <param name="folder">Thư mục phân nhóm file (mặc định "uploads")</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>Public URL truy cập file</returns>
    Task<string> UploadAsync(IFormFile file, string folder = "uploads", CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload file từ Stream lên MinIO.
    /// </summary>
    Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, string folder = "uploads", CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa file khỏi MinIO dựa trên URL file.
    /// Xử lý idempotent: nếu file không tồn tại trên MinIO, không throw exception.
    /// </summary>
    /// <param name="fileUrl">URL của file cần xóa</param>
    /// <param name="cancellationToken">CancellationToken</param>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra file có tồn tại trên MinIO hay không.
    /// </summary>
    /// <param name="fileUrl">URL của file cần kiểm tra</param>
    /// <param name="cancellationToken">CancellationToken</param>
    Task<bool> ExistsAsync(string fileUrl, CancellationToken cancellationToken = default);
}
