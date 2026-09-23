using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class StorageValidationTests
{
    [Theory]
    [InlineData("https://evil.example/culinary-blog/recipes/a.png")]
    [InlineData("http://localhost:9000/other-bucket/recipes/a.png")]
    [InlineData("http://localhost:9000/culinary-blog/recipes/a.png?x=1")]
    [InlineData("http://localhost:9000/culinary-blog/recipes/a.png#x")]
    [InlineData("http://localhost:9000/culinary-blog/recipes/../a.png")]
    [InlineData("http://localhost:9000/culinary-blog/recipes/%2e%2e/a.png")]
    [InlineData("recipes/a.png")]
    public async Task RejectsUntrustedDeleteUrlsBeforeContactingMinio(string url)
    {
        using var client = new MinioClient().WithEndpoint("localhost:9000").WithCredentials("test", "test").Build();
        using var storage = new MinioFileStorageService(client,
            Options.Create(new MinioOptions { BucketName = "culinary-blog" }), NullLogger<MinioFileStorageService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(url));
    }

    [Fact]
    public async Task StreamUploadCannotBypassImageValidation()
    {
        using var client = new MinioClient().WithEndpoint("localhost:9000").WithCredentials("test", "test").Build();
        using var storage = new MinioFileStorageService(client,
            Options.Create(new MinioOptions()), NullLogger<MinioFileStorageService>.Instance);
        using var stream = new MemoryStream("not-an-image"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.UploadStreamAsync(stream, "fake.png", "image/png"));
    }
}
