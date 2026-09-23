using System.Buffers.Binary;
using Microsoft.AspNetCore.Http;

namespace CulinaryBlog.Application.Common.Helpers;

public static class FileValidationHelper
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".webp"] = "image/webp", [".avif"] = "image/avif"
    };

    public static string ValidateFolder(string? folder)
    {
        var value = string.IsNullOrWhiteSpace(folder) ? "uploads" : folder.Trim();
        if (value.Length > 160 || value.Split('/').Any(segment =>
            segment.Length == 0 || segment.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')))
            throw new ArgumentException("Folder chỉ được chứa chữ ASCII, số, '-', '_' và '/' giữa các thư mục.");
        return value;
    }

    public static async Task ValidateImageFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Tệp tin tải lên không được rỗng.");
        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("Kích thước file vượt quá giới hạn 5MB.");
        if (!MimeByExtension.TryGetValue(Path.GetExtension(file.FileName), out var mime) ||
            !mime.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Phần mở rộng và MIME phải khớp định dạng JPEG, PNG, WebP hoặc AVIF.");
        using var stream = file.OpenReadStream();
        if (!await ValidateMagicBytesAsync(stream, mime, cancellationToken))
            throw new ArgumentException("Nội dung tệp tin không khớp định dạng ảnh (Magic Bytes validation failed).");
    }

    public static async Task<bool> ValidateMagicBytesAsync(Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead) return false;
        var position = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            var header = new byte[16];
            var count = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            switch (contentType.ToLowerInvariant())
            {
                case "image/jpeg":
                    return count >= 3 && header.AsSpan(0, 3).SequenceEqual(new byte[] { 0xff, 0xd8, 0xff });
                case "image/png":
                    return count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                case "image/webp":
                    return count >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                        && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
                case "image/avif":
                    if (count < 16 || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8)) return false;
                    var size = BinaryPrimitives.ReadUInt32BigEndian(header);
                    // Compatible brands start at offset 16. Never accept mif1 alone as AVIF.
                    if (size < 16 || size > MaxFileSizeBytes || (size - 16) % 4 != 0) return false;
                    if (stream.CanSeek && size > stream.Length) return false;
                    var avif = IsAvifBrand(header.AsSpan(8, 4));
                    var brand = new byte[4];
                    for (var offset = 16u; offset < size; offset += 4)
                    {
                        if (await stream.ReadAtLeastAsync(brand, 4, false, cancellationToken) != 4) return false;
                        avif |= IsAvifBrand(brand);
                    }
                    return avif;
                default:
                    return false;
            }
        }
        finally
        {
            if (stream.CanSeek) stream.Position = position;
        }
    }

    private static bool IsAvifBrand(ReadOnlySpan<byte> brand) =>
        brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);
}
