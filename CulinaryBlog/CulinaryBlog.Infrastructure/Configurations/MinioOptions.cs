namespace CulinaryBlog.Infrastructure.Configurations;

public class MinioOptions
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "culinaryblog";
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Base URL công khai để client truy cập trực tiếp file.
    /// Nếu để trống, sẽ tự động sinh dựa trên Endpoint và UseSsl: http://{Endpoint} hoặc https://{Endpoint}.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
