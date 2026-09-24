using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class LiveStorageTests
{
    [LiveFact]
    public async Task PostgreSqlMigrationAndMinioUploadPublicReadOwnershipDelete()
    {
        // Only resources created for this test are removed. Never use the application's database/bucket.
        var suffix = Guid.NewGuid().ToString("N");
        var databaseName = "culinary_file_test_" + suffix;
        var bucket = "culinary-file-test-" + suffix;
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("CULINARY_TEST_POSTGRES") ??
            "Host=localhost;Port=5432;Username=postgres;Password=postgres")
        { Database = databaseName };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        var options = new MinioOptions
        {
            Endpoint = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ENDPOINT") ?? "localhost:9000",
            AccessKey = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ACCESS_KEY") ?? "minioadmin",
            SecretKey = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_SECRET_KEY") ?? "minioadmin",
            BucketName = bucket
        };
        using var client = new MinioClient().WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey).WithTimeout(10000).Build();
        using var storage = new MinioFileStorageService(client, Options.Create(options), NullLogger<MinioFileStorageService>.Instance);
        string? url = null;
        try
        {
            await db.Database.MigrateAsync();
            var owner = new User { Id = Guid.NewGuid(), UserName = "file-owner", Email = "owner@example.test" };
            var other = new User { Id = Guid.NewGuid(), UserName = "file-other", Email = "other@example.test" };
            db.Users.AddRange(owner, other);
            await db.SaveChangesAsync();
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a4f8AAAAASUVORK5CYII=");
            using var stream = new MemoryStream(png);
            var file = new FormFile(stream, 0, png.Length, "file", "pixel.png")
            { Headers = new HeaderDictionary(), ContentType = "image/png" };
            var reference = storage.CreateReference("recipes", file.FileName);
            await storage.UploadAsync(file, reference);
            url = storage.GetPublicUrl(reference);
            db.StoredFiles.Add(new StoredFile { OwnerId = owner.Id, Url = url, BucketName = reference.BucketName, ObjectKey = reference.ObjectKey });
            await db.SaveChangesAsync();
            Assert.Equal(owner.Id, (await db.StoredFiles.SingleAsync()).OwnerId);
            using var http = new HttpClient();
            var publicResponse = await http.GetAsync(url);
            publicResponse.EnsureSuccessStatusCode();
            Assert.Equal("image/png", publicResponse.Content.Headers.ContentType?.MediaType);
            Assert.Equal(png, await publicResponse.Content.ReadAsByteArrayAsync());
            Assert.True(await storage.ExistsAsync(url));
            // Changing the public domain does not change the stored object identity.
            options.PublicBaseUrl = "https://changed.example.test";
            await storage.DeleteAsync(reference);
            await storage.DeleteAsync(reference);
            // Check storage-level idempotence independently of the database tombstone.
            await storage.DeleteAsync(url);
            Assert.False(await storage.ExistsAsync(url));
        }
        finally
        {
            try
            {
                if (url != null) await storage.DeleteAsync(url);
                if (await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
                    await client.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucket));
            }
            finally { await db.Database.EnsureDeletedAsync(); }
        }
    }

    private sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("CULINARY_LIVE_TESTS") != "1")
                Skip = "Set CULINARY_LIVE_TESTS=1 with PostgreSQL and MinIO running.";
        }
    }
}
