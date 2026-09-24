namespace CulinaryBlog.Infrastructure.Configurations;

public class MinioOptions
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "culinary-blog";
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Base URL công khai để client truy cập trực tiếp file.
    /// Nếu để trống, sẽ tự động sinh dựa trên Endpoint và UseSsl: http://{Endpoint} hoặc https://{Endpoint}.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
    public List<LegacyStorageLocation> LegacyLocations { get; set; } = [];
}

public sealed class LegacyStorageLocation
{
    // Full canonical URL prefix INCLUDING bucket, ending in '/'.
    public string PublicPrefix { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}
