using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Moq;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class ImageResizeJobPostgresTests
{
    [ImageStorageFact]
    public async Task Resize_reads_original_from_minio_and_stores_both_variants()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "image_minio_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        var connection = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = database,
            Pooling = false
        }.ConnectionString;
        var minioOptions = new MinioOptions
        {
            Endpoint = Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_ENDPOINT")!,
            AccessKey = Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_ACCESS_KEY")!,
            SecretKey = Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_SECRET_KEY")!,
            BucketName = "culinary-blog-integration-tests"
        };
        var client = new MinioClient()
            .WithEndpoint(minioOptions.Endpoint)
            .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
            .Build();
        using var storage = new MinioFileStorageService(client, Options.Create(minioOptions),
            NullLogger<MinioFileStorageService>.Instance);
        var imageId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var original = new StorageObjectReference(minioOptions.BucketName,
            $"recipes/{recipeId:N}/{imageId:N}.png");
        Assert.True(RecipeImageVariantKeys.TryCreate(original, out var medium, out var thumbnail));

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            using var source = new MagickImage(MagickColors.Blue, 1200, 900);
            source.Format = MagickFormat.Png;
            var bytes = source.ToByteArray();
            using (var originalStream = new MemoryStream(bytes))
                await storage.UploadStreamAsync(originalStream, "original.png", "image/png", original);

            var user = new User { Id = Guid.NewGuid(), UserName = "minio-author", FullName = "Minio Author", Email = "minio@example.com" };
            var category = new Category("Test", "image-minio-test", null) { Id = Guid.NewGuid() };
            db.Users.Add(user);
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId, Title = "MinIO test", Slug = "image-minio-test",
                AuthorId = user.Id, CategoryId = category.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10
            });
            var file = new StoredFile
            {
                Id = Guid.NewGuid(), OwnerId = user.Id, Url = storage.GetPublicUrl(original),
                BucketName = original.BucketName, ObjectKey = original.ObjectKey,
                Status = StoredFileStatus.Active, SizeBytes = bytes.Length
            };
            db.StoredFiles.Add(file);
            db.RecipeImages.Add(new RecipeImage
            {
                Id = imageId, RecipeId = recipeId, StoredFileId = file.Id,
                OriginalUrl = file.Url, IsPrimary = true
            });
            await db.SaveChangesAsync();

            await new ImageResizeJob(db, storage, NullLogger<ImageResizeJob>.Instance)
                .ExecuteAsync(imageId, CancellationToken.None);
            db.ChangeTracker.Clear();
            var image = await db.RecipeImages.AsNoTracking().SingleAsync(x => x.Id == imageId);
            Assert.Equal(storage.GetPublicUrl(medium), image.MediumUrl);
            Assert.Equal(storage.GetPublicUrl(thumbnail), image.ThumbnailUrl);
            Assert.Equal(bytes, await storage.DownloadBytesAsync(original));

            var mediumInfo = new MagickImageInfo(await storage.DownloadBytesAsync(medium));
            var thumbnailInfo = new MagickImageInfo(await storage.DownloadBytesAsync(thumbnail));
            Assert.Equal((800u, 600u), (mediumInfo.Width, mediumInfo.Height));
            Assert.Equal((300u, 300u), (thumbnailInfo.Width, thumbnailInfo.Height));
            Assert.Equal(MagickFormat.WebP, mediumInfo.Format);
            Assert.Equal(MagickFormat.WebP, thumbnailInfo.Format);
        }
        finally
        {
            try
            {
                await storage.DeleteAsync(thumbnail);
                await storage.DeleteAsync(medium);
                await storage.DeleteAsync(original);
            }
            finally
            {
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    [PostgresFact]
    public async Task Reconciliation_recovers_enqueue_failure_and_resize_updates_urls_after_both_uploads()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "image_resize_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = database,
                Pooling = false
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();

            var user = new User { Id = Guid.NewGuid(), UserName = "image-author", FullName = "Image Author", Email = "image@example.com" };
            var category = new Category("Test", "image-resize-test", null) { Id = Guid.NewGuid() };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = "Image test", Slug = "image-resize-test",
                AuthorId = user.Id, CategoryId = category.Id,
                PrepTimeMinutes = 10, CookingTimeMinutes = 10
            };
            var imageId = Guid.NewGuid();
            var original = new StorageObjectReference("culinary-blog", $"recipes/{recipe.Id:N}/{imageId:N}.png");
            var storage = new MemoryImageStorage();
            using (var source = new MagickImage(MagickColors.Red, 1200, 900))
            {
                source.Format = MagickFormat.Png;
                storage.Objects[original.ObjectKey] = source.ToByteArray();
            }
            var file = new StoredFile
            {
                Id = Guid.NewGuid(), OwnerId = user.Id, Url = storage.GetPublicUrl(original),
                BucketName = original.BucketName, ObjectKey = original.ObjectKey,
                Status = StoredFileStatus.Active, SizeBytes = storage.Objects[original.ObjectKey].Length
            };
            db.Users.Add(user);
            db.Categories.Add(category);
            db.Recipes.Add(recipe);
            db.StoredFiles.Add(file);
            db.RecipeImages.Add(new RecipeImage
            {
                Id = imageId, RecipeId = recipe.Id, StoredFileId = file.Id,
                OriginalUrl = file.Url, IsPrimary = true
            });
            await db.SaveChangesAsync();

            var jobs = new Mock<IBackgroundJobClient>();
            jobs.Setup(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Throws(new IOException("Hangfire unavailable"));
            var queue = new HangfireImageResizeQueue(jobs.Object, db);
            var reconciliation = new ImageResizeReconciliationJob(db, queue,
                NullLogger<ImageResizeReconciliationJob>.Instance);
            await reconciliation.ExecuteAsync(CancellationToken.None);
            Assert.Null((await db.RecipeImages.AsNoTracking().SingleAsync(x => x.Id == imageId)).ResizeJobId);

            jobs.Reset();
            jobs.Setup(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("resize-1");
            await reconciliation.ExecuteAsync(CancellationToken.None);
            Assert.Equal("resize-1", (await db.RecipeImages.AsNoTracking().SingleAsync(x => x.Id == imageId)).ResizeJobId);
            jobs.Verify(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
            await reconciliation.ExecuteAsync(CancellationToken.None);
            jobs.Verify(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);

            var resize = new ImageResizeJob(db, storage, NullLogger<ImageResizeJob>.Instance);
            storage.FailThumbnailOnce = true;
            await Assert.ThrowsAsync<IOException>(() => resize.ExecuteAsync(imageId, CancellationToken.None));
            db.ChangeTracker.Clear();
            var failed = await db.RecipeImages.AsNoTracking().SingleAsync(x => x.Id == imageId);
            Assert.Null(failed.MediumUrl);
            Assert.Null(failed.ThumbnailUrl);
            Assert.True(storage.Objects.ContainsKey(original.ObjectKey));

            await resize.ExecuteAsync(imageId, CancellationToken.None);
            db.ChangeTracker.Clear();
            var completed = await db.RecipeImages.AsNoTracking().SingleAsync(x => x.Id == imageId);
            Assert.NotNull(completed.MediumUrl);
            Assert.NotNull(completed.ThumbnailUrl);
            Assert.Equal(file.Url, completed.OriginalUrl);
            Assert.True(RecipeImageVariantKeys.TryCreate(original, out var medium, out var thumbnail));
            var mediumInfo = new MagickImageInfo(storage.Objects[medium.ObjectKey]);
            var thumbnailInfo = new MagickImageInfo(storage.Objects[thumbnail.ObjectKey]);
            Assert.Equal(800u, mediumInfo.Width);
            Assert.Equal(600u, mediumInfo.Height);
            Assert.Equal(300u, thumbnailInfo.Width);
            Assert.Equal(300u, thumbnailInfo.Height);
            Assert.Equal(MagickFormat.WebP, mediumInfo.Format);
            Assert.Equal(MagickFormat.WebP, thumbnailInfo.Format);
            Assert.Equal(3, storage.Objects.Count);
            Assert.Single(await db.RecipeCacheInvalidations.Where(x => x.RecipeSlug == recipe.Slug).ToListAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class MemoryImageStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public bool FailThumbnailOnce { get; set; }

        public StorageObjectReference CreateReference(string folder, string fileName) =>
            throw new NotSupportedException();

        public string GetPublicUrl(StorageObjectReference reference) =>
            $"http://minio.test/{reference.BucketName}/{reference.ObjectKey}";

        public Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task UploadStreamAsync(Stream stream, string fileName, string contentType,
            StorageObjectReference reference, CancellationToken ct = default)
        {
            if (FailThumbnailOnce && reference.ObjectKey.EndsWith("-thumbnail.webp", StringComparison.Ordinal))
            {
                FailThumbnailOnce = false;
                throw new IOException("Thumbnail upload unavailable");
            }
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, ct);
            Objects[reference.ObjectKey] = output.ToArray();
        }

        public Task DeleteAsync(StorageObjectReference reference, CancellationToken ct = default)
        {
            Objects.Remove(reference.ObjectKey);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken ct = default) =>
            Task.FromResult(Objects.ContainsKey(reference.ObjectKey));

        public Task<byte[]> DownloadBytesAsync(StorageObjectReference reference, CancellationToken ct = default) =>
            Task.FromResult(Objects[reference.ObjectKey]);
    }
}

public sealed class ImageStorageFactAttribute : FactAttribute
{
    public ImageStorageFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_ENDPOINT")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_ACCESS_KEY")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IMAGE_TEST_MINIO_SECRET_KEY")))
        {
            Skip = "Set SEARCH_TEST_POSTGRES and IMAGE_TEST_MINIO_* to run the MinIO image resize integration test.";
        }
    }
}
