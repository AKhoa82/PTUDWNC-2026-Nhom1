using Microsoft.AspNetCore.Http;

namespace CulinaryBlog.Application.Common.Helpers;

public static class FileValidationHelper
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/avif"
    };

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".avif"
    };

    /// <summary>
    /// Validate file theo các quy tắc trong SRS (kích thước, định dạng, magic bytes).
    /// </summary>
    public static async Task ValidateImageFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tin tải lên không được rỗng.", nameof(file));
        }

        // 1. Kiểm tra kích thước file (tối đa 5MB)
        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"Kích thước file vượt quá giới hạn 5MB. (Dung lượng: {file.Length / (1024.0 * 1024.0):F2}MB)");
        }

        // 2. Kiểm tra Extension
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"Phần mở rộng '{extension}' không được hỗ trợ. Chỉ hỗ trợ .jpg, .jpeg, .png, .webp, .avif.");
        }

        // 3. Kiểm tra MIME Type
        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"Định dạng MIME type '{file.ContentType}' không hợp lệ. Chỉ chấp nhận image/jpeg, image/png, image/webp, image/avif.");
        }

        // 4. Kiểm tra Magic Bytes
        using var stream = file.OpenReadStream();
        var isValidMagicBytes = await ValidateMagicBytesAsync(stream, file.ContentType, cancellationToken);
        if (!isValidMagicBytes)
        {
            throw new ArgumentException("Nội dung tệp tin không khớp với định dạng ảnh hợp lệ (Magic Bytes validation failed).");
        }
    }

    /// <summary>
    /// Đọc các byte đầu tiên của stream để kiểm tra magic bytes tương ứng với MIME type.
    /// </summary>
    public static async Task<bool> ValidateMagicBytesAsync(Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead)
            return false;

        var currentPosition = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        var headerBytes = new byte[16];
        var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length, cancellationToken);

        if (stream.CanSeek)
            stream.Seek(currentPosition, SeekOrigin.Begin);

        if (bytesRead < 4)
            return false;

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => IsJpeg(headerBytes),
            "image/png" => IsPng(headerBytes),
            "image/webp" => IsWebP(headerBytes, bytesRead),
            "image/avif" => IsAvif(headerBytes, bytesRead),
            _ => false
        };
    }

    private static bool IsJpeg(byte[] header)
    {
        // JPEG bắt đầu bằng FF D8 FF
        return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    }

    private static bool IsPng(byte[] header)
    {
        // PNG bắt đầu bằng 89 50 4E 47
        return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
    }

    private static bool IsWebP(byte[] header, int bytesRead)
    {
        // WebP: offset 0..3 = "RIFF" (0x52, 0x49, 0x46, 0x46)
        // offset 8..11 = "WEBP" (0x57, 0x45, 0x42, 0x50)
        if (bytesRead < 12) return false;

        bool hasRiff = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x47;
        bool hasWebp = header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        return hasRiff && hasWebp;
    }

    private static bool IsAvif(byte[] header, int bytesRead)
    {
        // AVIF là container ISOBMFF. Offset 4..7 phải là "ftyp" (0x66, 0x74, 0x79, 0x70)
        // và offset 8..11 là brand "avif" hoặc "mif1"
        if (bytesRead < 12) return false;

        bool hasFtyp = header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
        if (!hasFtyp) return false;

        string brand = System.Text.Encoding.ASCII.GetString(header, 8, 4);
        return brand.Equals("avif", StringComparison.OrdinalIgnoreCase) ||
               brand.Equals("avis", StringComparison.OrdinalIgnoreCase) ||
               brand.Equals("mif1", StringComparison.OrdinalIgnoreCase);
    }
}
