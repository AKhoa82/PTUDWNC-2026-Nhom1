using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Images;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class LiveRecipeImageTests
{
    [RecipeLiveFact]
    public async Task MaintenanceCliRunsWithoutJwtHttpOrHangfireAndDryRunDoesNotMutate()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await using var db = fixture.NewDb();
        var reference = fixture.Storage.CreateReference("recipes", "legacy.webp");
        db.StoredFiles.Add(new StoredFile { OwnerId = fixture.Owner.Id, Url = fixture.Storage.GetPublicUrl(reference) });
        await db.SaveChangesAsync();
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(typeof(RecipeImageEndpoints).Assembly.Location);
        start.ArgumentList.Add("--storage-backfill");
        foreach (var entry in fixture.ApiEnvironment()) start.Environment[entry.Key] = entry.Value;
        start.Environment["Jwt__Key"] = "";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode == 0, await stderr);
            var lines = (await stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.Equal("candidate", JsonDocument.Parse(lines[0]).RootElement.GetProperty("result").GetString());
            db.ChangeTracker.Clear();
            Assert.Null((await db.StoredFiles.SingleAsync()).ObjectKey);
            var schemas = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.schemata WHERE schema_name = 'hangfire'").SingleAsync();
            Assert.Equal(0, schemas);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
    }

    [RecipeLiveFact]
    public async Task CacheOutboxSurvivesFailureAndWorkerInvalidatesSharedRedisOnRetry()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var prefix = fixture.OptionsConfig.BucketName + ":cache-retry:";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOutputCache();
        services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = Environment.GetEnvironmentVariable("CULINARY_TEST_REDIS") ?? "localhost:6379";
            o.InstanceName = prefix + "list:";
        });
        services.AddStackExchangeRedisOutputCache(o =>
        {
            o.Configuration = Environment.GetEnvironmentVariable("CULINARY_TEST_REDIS") ?? "localhost:6379";
            o.InstanceName = prefix + "output:";
        });
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var output = provider.GetRequiredService<IOutputCacheStore>();
        await using var db = fixture.NewDb();
        var intent = new RecipeCacheInvalidation();
        db.RecipeCacheInvalidations.Add(intent);
        await db.SaveChangesAsync();
        await output.SetAsync("detail", [1, 2, 3], ["recipes"], TimeSpan.FromMinutes(1), default);
        var failing = new RecipeImageCacheInvalidator(db, output, new UnavailableCache(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageCacheInvalidator>.Instance);
        await failing.InvalidateAsync(); // Mutation caller succeeds despite cache failure.
        await using (var check = fixture.NewDb())
            Assert.Null((await check.RecipeCacheInvalidations.SingleAsync()).ProcessedAt);
        // Simulate stale cached data restored while Redis was recovering.
        await output.SetAsync("detail", [4, 5, 6], ["recipes"], TimeSpan.FromMinutes(1), default);
        await using (var retryDb = fixture.NewDb())
            await new RecipeImageCacheInvalidator(retryDb, output, cache,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageCacheInvalidator>.Instance).ProcessAsync(default);
        Assert.Null(await output.GetAsync("detail", default));
        Assert.NotNull(await cache.GetStringAsync(GetRecipesQueryHandler.CacheVersionKey));
        await using var verified = fixture.NewDb();
        Assert.NotNull((await verified.RecipeCacheInvalidations.SingleAsync()).ProcessedAt);
        await cache.RemoveAsync(GetRecipesQueryHandler.CacheVersionKey);
    }

    private sealed class UnavailableCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new IOException("Redis unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new IOException("Redis unavailable");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new IOException("Redis unavailable");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new IOException("Redis unavailable");
        public void Refresh(string key) => throw new IOException("Redis unavailable");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new IOException("Redis unavailable");
        public void Remove(string key) => throw new IOException("Redis unavailable");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new IOException("Redis unavailable");
    }

    [RecipeLiveFact]
    public async Task BackfillIsExplicitIdempotentAndInventoryNeverDeletesUntrackedObjects()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await using var db = fixture.NewDb();
        var reference = fixture.Storage.CreateReference("recipes", "old.webp");
        var oldUrl = fixture.Storage.GetPublicUrl(reference);
        var known = new StoredFile { OwnerId = fixture.Owner.Id, Url = oldUrl };
        var unknown = new StoredFile { OwnerId = fixture.Owner.Id, Url = "https://external.test/other.webp",
            Status = StoredFileStatus.DeletePending, DeletionRequestedAt = DateTime.UtcNow };
        db.StoredFiles.AddRange(known, unknown);
        db.RecipeImages.Add(new RecipeImage { RecipeId = fixture.Recipe.Id, OriginalUrl = "https://external.test/legacy.webp", IsPrimary = true });
        await db.SaveChangesAsync();
        var untracked = fixture.Storage.CreateReference("recipes", "untracked.webp");
        await fixture.Storage.UploadAsync(Image(), untracked);
        fixture.OptionsConfig.PublicBaseUrl = "https://new-public.example.test";
        fixture.OptionsConfig.LegacyLocations.Add(new LegacyStorageLocation
        { PublicPrefix = oldUrl[..^reference.ObjectKey.Length], BucketName = reference.BucketName });
        var maintenance = fixture.Maintenance(db);
        Assert.Null(maintenance.Resolve("https://evil.test/" + reference.BucketName + "/" + reference.ObjectKey));
        Assert.Null(maintenance.Resolve(oldUrl + "?download=1"));
        Assert.Null(maintenance.Resolve(oldUrl[..^reference.ObjectKey.Length] + "../a.webp"));
        using var dryReport = new StringWriter();
        await maintenance.BackfillAsync(false, dryReport, default);
        Assert.Contains("candidate", dryReport.ToString());
        Assert.All(await db.StoredFiles.ToListAsync(), file => Assert.Null(file.ObjectKey));
        await maintenance.BackfillAsync(true, new StringWriter(), default);
        await maintenance.BackfillAsync(true, new StringWriter(), default);
        var mapped = await db.StoredFiles.SingleAsync(x => x.Id == known.Id);
        Assert.Equal(reference.ObjectKey, mapped.ObjectKey);
        Assert.Equal(reference.BucketName, mapped.BucketName);
        Assert.Equal(2, await db.StoredFiles.CountAsync());
        Assert.Null((await db.RecipeImages.SingleAsync()).StoredFileId);
        var job = new DeleteStoredFileJob(db, fixture.Faults, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteStoredFileJob>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(unknown.Id, default));
        Assert.Equal(0, fixture.Faults.DeleteCalls);
        using var report = new StringWriter();
        await maintenance.InventoryAsync(report, default);
        Assert.Contains(untracked.ObjectKey, report.ToString());
        Assert.True(await fixture.Storage.ExistsAsync(untracked));
    }

    [RecipeLiveFact]
    public async Task LifecycleMigrationPreservesLegacyActivePendingAndDeletedStates()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.NewDb();
        await db.GetService<IMigrator>().MigrateAsync("20260923161609_AddRecipeImagesAndFileDeletionQueue");
        db.Users.Add(fixture.Owner);
        await db.SaveChangesAsync();
        var urls = new[] { "https://legacy.test/active", "https://legacy.test/pending", "https://legacy.test/deleted" };
        for (var index = 0; index < urls.Length; index++)
        {
            DateTime? requested = index >= 1 ? DateTime.UtcNow : null;
            DateTime? deleted = index == 2 ? DateTime.UtcNow : null;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "StoredFiles" ("Id", "OwnerId", "Url", "SizeBytes", "CreatedAt", "DeletionRequestedAt", "DeletedAt")
                VALUES ({Guid.NewGuid()}, {fixture.Owner.Id}, {urls[index]}, 12, now(), {requested}, {deleted})
                """);
        }
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(StoredFileStatus.Active, (await db.StoredFiles.SingleAsync(x => x.Url == urls[0])).Status);
        Assert.Equal(StoredFileStatus.DeletePending, (await db.StoredFiles.SingleAsync(x => x.Url == urls[1])).Status);
        Assert.Equal(StoredFileStatus.Deleted, (await db.StoredFiles.SingleAsync(x => x.Url == urls[2])).Status);
        Assert.All(await db.StoredFiles.ToListAsync(), x => Assert.Null(x.UploadExpiresAt));
    }

    [RecipeLiveFact]
    public async Task LostCommitAcknowledgementPreservesImageAndRecoveryReturnsOriginalDto()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await using var db = fixture.NewDb();
        var sessions = new FaultSessionFactory(fixture.Services.GetRequiredService<IFileLifecycleSessionFactory>(), true, false);
        var service = new RecipeImageService(db, fixture.Faults, new TestDeletionQueue(), new RecipeImageTransactionFactory(db),
            sessions, Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageService>.Instance);
        var dto = await service.UploadAsync(fixture.Recipe.Id, Image(), null, false, fixture.Owner.Id.ToString(), false, default);
        var image = await db.RecipeImages.SingleAsync();
        Assert.Equal(dto.ImageId, image.Id);
        Assert.True(await fixture.Storage.ExistsAsync(dto.OriginalUrl));
        Assert.Equal(StoredFileStatus.Active, (await db.StoredFiles.SingleAsync()).Status);
        Assert.Single(await db.RecipeCacheInvalidations.Where(x => x.ProcessedAt == null).ToListAsync());
        Assert.Equal(0, fixture.Faults.DeleteCalls);
    }

    [RecipeLiveFact]
    public async Task UnknownCommittedOutcomeDoesNotDeleteAndReconciliationPreservesAttachedFile()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await using var db = fixture.NewDb();
        var service = new RecipeImageService(db, fixture.Faults, new TestDeletionQueue(), new RecipeImageTransactionFactory(db),
            new FaultSessionFactory(fixture.Services.GetRequiredService<IFileLifecycleSessionFactory>(), true, true),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageService>.Instance);
        await Assert.ThrowsAsync<FileOperationUnavailableException>(() => service.UploadAsync(fixture.Recipe.Id, Image(), null, false, fixture.Owner.Id.ToString(), false, default));
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
        var image = await db.RecipeImages.SingleAsync();
        Assert.True(await fixture.Storage.ExistsAsync(image.OriginalUrl));
        Assert.Equal(0, fixture.Faults.DeleteCalls);
    }

    [RecipeLiveFact]
    public async Task AbandonedUploadWithUnavailableRecoveryIsDurablyCleanedAfterExpiry()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        fixture.SaveFailure.Enabled = true;
        await using var db = fixture.NewDb();
        var service = new RecipeImageService(db, fixture.Faults, new TestDeletionQueue(), new RecipeImageTransactionFactory(db),
            new FaultSessionFactory(fixture.Services.GetRequiredService<IFileLifecycleSessionFactory>(), false, true),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageService>.Instance);
        await Assert.ThrowsAsync<FileOperationUnavailableException>(() => service.UploadAsync(fixture.Recipe.Id, Image(), null, false, fixture.Owner.Id.ToString(), false, default));
        var file = await db.StoredFiles.SingleAsync();
        Assert.Equal(StoredFileStatus.PendingUpload, file.Status);
        Assert.True(await fixture.Storage.ExistsAsync(file.Url));
        file.UploadExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
        await fixture.RunWorkerUntilDeletedAsync(1);
        Assert.False(await fixture.Storage.ExistsAsync(file.Url));
        Assert.Empty(await db.RecipeImages.ToListAsync());
    }

    [RecipeLiveFact]
    public async Task ExpiredUploadCleanupSkipsLockedFileAndNeverDeletesActiveImage()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        using var scope = fixture.Services.CreateScope();
        var active = await scope.ServiceProvider.GetRequiredService<RecipeImageService>().UploadAsync(
            fixture.Recipe.Id, Image(), null, false, fixture.Owner.Id.ToString(), false, default);
        var reference = fixture.Storage.CreateReference("recipes", "pending.webp");
        var pending = new StoredFile { OwnerId = fixture.Owner.Id, BucketName = reference.BucketName, ObjectKey = reference.ObjectKey,
            Url = fixture.Storage.GetPublicUrl(reference), Status = StoredFileStatus.PendingUpload, UploadExpiresAt = DateTime.UtcNow.AddHours(-2) };
        await using var db = fixture.NewDb();
        db.StoredFiles.Add(pending);
        await db.SaveChangesAsync();
        await fixture.Storage.UploadAsync(Image(), reference);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StoredFiles\" WHERE \"Id\" = {pending.Id} FOR UPDATE");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(timeout.Token);
            Assert.True(await fixture.Storage.ExistsAsync(reference));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries<StoredFile>());
        }
        await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
        await fixture.RunWorkerUntilDeletedAsync(1);
        Assert.False(await fixture.Storage.ExistsAsync(reference));
        Assert.True(await fixture.Storage.ExistsAsync(active.OriginalUrl));
    }

    [RecipeLiveFact]
    public async Task PutAcceptedThenConnectionFailureLeavesKnownReferenceForCleanup()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        fixture.Faults.FailAfterUpload = true;
        using var scope = fixture.Services.CreateScope();
        await Assert.ThrowsAsync<FileOperationUnavailableException>(() => scope.ServiceProvider.GetRequiredService<RecipeImageService>()
            .UploadAsync(fixture.Recipe.Id, Image(), null, false, fixture.Owner.Id.ToString(), false, default));
        await using var db = fixture.NewDb();
        var file = await db.StoredFiles.SingleAsync();
        Assert.Equal(StoredFileStatus.DeletePending, file.Status);
        Assert.True(await fixture.Storage.ExistsAsync(file.Url));
        await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
        await fixture.RunWorkerUntilDeletedAsync(1);
        Assert.False(await fixture.Storage.ExistsAsync(file.Url));
    }

    private sealed class FaultSessionFactory(IFileLifecycleSessionFactory inner, bool loseCommit, bool failRecovery) : IFileLifecycleSessionFactory
    {
        private int _count;
        public IFileLifecycleSession Create()
        {
            var count = ++_count;
            if (count >= 3 && failRecovery) throw new IOException("Database unavailable during verification");
            return new Session(inner.Create(), count == 2 && loseCommit);
        }
        private sealed class Session(IFileLifecycleSession inner, bool loseCommit) : IFileLifecycleSession
        {
            public IApplicationDbContext Db => inner.Db;
            public async Task<IRecipeImageTransaction> BeginAsync(Guid? recipeId, Guid fileId, CancellationToken ct) =>
                new Transaction(await inner.BeginAsync(recipeId, fileId, ct), loseCommit);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
        private sealed class Transaction(IRecipeImageTransaction inner, bool loseCommit) : IRecipeImageTransaction
        {
            public async Task CommitAsync(CancellationToken ct)
            {
                await inner.CommitAsync(ct);
                if (loseCommit) throw new IOException("Lost COMMIT acknowledgement");
            }
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    [RecipeLiveFact]
    public async Task RealApplicationStartupAuthDashboardAndFullImageHttpLifecycle()
    {
        await using var fixture = new Fixture();
        fixture.Recipe.Status = RecipeStatus.Published;
        fixture.Recipe.Title = fixture.Recipe.Id.ToString("N");
        await fixture.InitializeAsync();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(typeof(RecipeImageEndpoints).Assembly.Location);
        foreach (var pair in fixture.ApiEnvironment()) start.Environment[pair.Key] = pair.Value;
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Process? peer = null;
        Task<string>? peerStdout = null;
        Task<string>? peerStderr = null;
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            while (true)
            {
                if (process.HasExited) throw new InvalidOperationException(await stdout + await stderr);
                try { await http.GetAsync("/api/v1/files/exists", timeout.Token); break; }
                catch (HttpRequestException) { await Task.Delay(100, timeout.Token); }
            }
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/v1/files/exists?fileUrl=x")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync("/api/v1/files/upload", RecipeImageEndpointTests.ImageForm())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync("/api/v1/files?fileUrl=x")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/hangfire")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/v1/recipes", new { })).StatusCode);
            var path = $"/api/v1/recipes/{fixture.Recipe.Id}/images";
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync(path, RecipeImageEndpointTests.ImageForm())).StatusCode);

            var register = await http.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                Username = "httpauthor", Email = "http@example.test", FullName = "HTTP Author",
                Password = "Test-only-Password123!", ConfirmPassword = "Test-only-Password123!", TermsAccepted = true
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);
            var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
            var login = new LoginRequest { Email = "http@example.test", Password = "Test-only-Password123!" };
            async Task SignIn()
            {
                var response = await http.PostAsJsonAsync("/api/auth/login", login);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("accessToken").GetString();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            await SignIn();
            var createRequest = new CreateRecipeRequest
            {
                Title = "Authenticated creation", CategoryId = fixture.Recipe.CategoryId,
                Instructions = "Cook", Servings = 1
            };
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync("/api/v1/recipes", createRequest)).StatusCode);
            createRequest.Ingredients.Add(new CreateIngredientRequestDto { Name = "Rice", Quantity = 1, Unit = "cup" });
            createRequest.Steps.Add(new CreateStepRequestDto { Description = "Cook the rice" });
            var created = await http.PostAsJsonAsync("/api/v1/recipes", createRequest);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var createdId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await using (var check = fixture.NewDb())
                Assert.Equal(userId.ToString(), (await check.Recipes.SingleAsync(x => x.Id == createdId)).AuthorId);
            createRequest.ImageUrl = "https://example.test/" + new string('a', 501);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync("/api/v1/recipes", createRequest)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/hangfire")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync(path, RecipeImageEndpointTests.ImageForm())).StatusCode);
            await using (var db = fixture.NewDb())
            {
                var adminRole = await db.Roles.SingleAsync(x => x.Name == "Admin");
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = userId, RoleId = adminRole.Id });
                await db.SaveChangesAsync();
            }
            await SignIn(); // Role comes from the real login flow, not a hand-built token.
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/hangfire/")).StatusCode);
            // A second real API process shares Redis/PostgreSQL; reads here must reflect writes on A.
            using var peerListener = new TcpListener(IPAddress.Loopback, 0);
            peerListener.Start();
            var peerPort = ((IPEndPoint)peerListener.LocalEndpoint).Port;
            peerListener.Stop();
            start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{peerPort}";
            peer = Process.Start(start)!;
            peerStdout = peer.StandardOutput.ReadToEndAsync();
            peerStderr = peer.StandardError.ReadToEndAsync();
            using var publicReader = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{peerPort}") };
            while (true)
            {
                if (peer.HasExited) throw new InvalidOperationException(await peerStdout + await peerStderr);
                try { await publicReader.GetAsync("/api/v1/files/exists", timeout.Token); break; }
                catch (HttpRequestException) { await Task.Delay(100, timeout.Token); }
            }
            var detailPath = "/api/v1/recipes/" + fixture.Recipe.Slug;
            var listPath = "/api/v1/recipes?keyword=" + fixture.Recipe.Title;
            Assert.Empty((await publicReader.GetFromJsonAsync<RecipeDetailDto>(detailPath))!.Images);
            Assert.Null((await publicReader.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>(listPath))!.Items.Single().ImageUrl);
            var uploaded = await http.PostAsync(path, RecipeImageEndpointTests.ImageForm());
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
            var image = (await uploaded.Content.ReadFromJsonAsync<RecipeImageDto>())!;
            Assert.Equal(Encoding.ASCII.GetBytes("RIFFxxxxWEBP"), await publicReader.GetByteArrayAsync(image.OriginalUrl));
            Assert.Equal(image.OriginalUrl, (await publicReader.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>(listPath))!.Items.Single().ImageUrl);
            Assert.Equal(HttpStatusCode.OK, (await http.PatchAsJsonAsync(path + "/" + image.ImageId, new { isPrimary = true, altText = "Dinner" })).StatusCode);
            Assert.Equal("Dinner", (await publicReader.GetFromJsonAsync<RecipeDetailDto>(detailPath))!.Images.Single().AltText);
            Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync(path + "/" + image.ImageId)).StatusCode);
            Assert.Empty((await publicReader.GetFromJsonAsync<RecipeDetailDto>(detailPath))!.Images);
            Assert.Null((await publicReader.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>(listPath))!.Items.Single().ImageUrl);
            while (await fixture.Storage.ExistsAsync(image.OriginalUrl)) await Task.Delay(100, timeout.Token);
        }
        catch (Exception ex)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            var diagnostics = await stdout + await stderr;
            throw new InvalidOperationException("API integration failed. Server output:\n" + diagnostics[..Math.Min(diagnostics.Length, 16000)], ex);
        }
        finally
        {
            if (peer != null)
            {
                if (!peer.HasExited) peer.Kill(entireProcessTree: true);
                await peer.WaitForExitAsync();
                await Task.WhenAll(peerStdout!, peerStderr!);
                peer.Dispose();
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
    }

    [RecipeLiveFact]
    public async Task MigrationBackfillsLegacyAndEnforcesPrimaryIndexAndCascade()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.NewDb();
        await db.GetService<IMigrator>().MigrateAsync("20260923103049_AddStoredFileOwnership");
        var recipeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Categories\" (\"Id\", \"Name\", \"Slug\", \"CreatedAt\") VALUES ({categoryId}, 'Legacy', 'legacy', now())");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Recipes" ("Id", "Title", "Slug", "ImageUrl", "PrepTimeMinutes", "CookingTimeMinutes", "Servings", "Difficulty", "Status", "Instructions", "CategoryId", "CreatedAt")
            VALUES ({recipeId}, 'Legacy', 'legacy', 'https://example.test/old.png', 0, 0, 1, 1, 1, '', {categoryId}, now())
            """);
        await db.Database.MigrateAsync();
        var legacy = await db.RecipeImages.SingleAsync();
        Assert.True(legacy.IsPrimary);
        Assert.Null(legacy.StoredFileId);
        Assert.Equal("https://example.test/old.png", legacy.OriginalUrl);
        Assert.Empty(await db.StoredFiles.ToListAsync());
        db.RecipeImages.Add(new RecipeImage { RecipeId = recipeId, OriginalUrl = "https://example.test/duplicate.png", IsPrimary = true });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)error.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        await db.Recipes.Where(x => x.Id == recipeId).ExecuteDeleteAsync();
        Assert.Empty(await db.RecipeImages.ToListAsync());

        var newLegacyRecipe = new Recipe { Title = "Old create contract", Slug = "old-create", CategoryId = categoryId, ImageUrl = "https://example.test/new-legacy.png" };
        db.Recipes.Add(newLegacyRecipe);
        await db.SaveChangesAsync();
        var seededImage = await db.RecipeImages.SingleAsync();
        Assert.Equal(newLegacyRecipe.ImageUrl, seededImage.OriginalUrl);
        Assert.True(seededImage.IsPrimary);
        Assert.Null(seededImage.StoredFileId);
    }

    [RecipeLiveFact]
    public async Task ConcurrentUploadsPrimarySwitchAndDatabaseFailureAreAtomic()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var images = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<RecipeImageService>().UploadAsync(
                fixture.Recipe.Id, Image(), null, true, fixture.Owner.Id.ToString(), false, default);
        }));
        await Task.WhenAll(images.Select(async image =>
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RecipeImageService>().UpdateAsync(fixture.Recipe.Id, image.ImageId,
                new UpdateRecipeImageRequest { IsPrimary = true }, fixture.Owner.Id.ToString(), false, default);
        }));
        await using (var db = fixture.NewDb())
        {
            var primary = await db.RecipeImages.SingleAsync(x => x.IsPrimary);
            Assert.Equal(primary.OriginalUrl, (await db.Recipes.SingleAsync()).ImageUrl);
            Assert.Equal(Enumerable.Range(0, 6), await db.RecipeImages.OrderBy(x => x.OrderIndex).Select(x => x.OrderIndex).ToArrayAsync());
            var detail = await new GetRecipeBySlugQueryHandler(db).Handle(new GetRecipeBySlugQuery(fixture.Recipe.Slug, fixture.Owner.Id.ToString(), false), default);
            Assert.Equal(6, detail!.Images.Count);
            Assert.Equal(Enumerable.Range(0, 6), detail.Images.Select(x => x.OrderIndex));
        }

        // Fail only the final save, after MinIO has accepted the upload and old primary was demoted.
        fixture.SaveFailure.Enabled = true;
        await using (var failingDb = fixture.NewDb())
        {
            var observed = new ObservedStorage(fixture.Storage);
            var service = new RecipeImageService(failingDb, observed, new TestDeletionQueue(),
                new RecipeImageTransactionFactory(failingDb), fixture.Services.GetRequiredService<IFileLifecycleSessionFactory>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeImageService>.Instance);
            await Assert.ThrowsAsync<FileOperationUnavailableException>(() => service.UploadAsync(fixture.Recipe.Id, Image(), null, true, fixture.Owner.Id.ToString(), false, default));
            Assert.NotNull(observed.UploadedUrl);
            Assert.True(await fixture.Storage.ExistsAsync(observed.UploadedUrl!));
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
            await fixture.RunWorkerUntilDeletedAsync(1);
            Assert.False(await fixture.Storage.ExistsAsync(observed.UploadedUrl!));
        }
        fixture.SaveFailure.Enabled = false;
        await using (var db = fixture.NewDb())
        {
            Assert.Equal(6, await db.RecipeImages.CountAsync());
            var primary = await db.RecipeImages.SingleAsync(x => x.IsPrimary);
            Assert.Equal(primary.OriginalUrl, (await db.Recipes.SingleAsync()).ImageUrl);
        }
        // Clean all objects through the same durable deletion path.
        foreach (var image in images)
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RecipeImageService>().DeleteAsync(fixture.Recipe.Id, image.ImageId, fixture.Owner.Id.ToString(), false, default);
        }
        await fixture.RunWorkerUntilDeletedAsync(images.Length + 1);
    }

    [RecipeLiveFact]
    public async Task MinioPublicReadDurableHangfireDeletionAndReconciliation()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        Guid storedId;
        string url;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<RecipeImageService>();
            var image = await service.UploadAsync(fixture.Recipe.Id, Image(), "Food", false, fixture.Owner.Id.ToString(), false, default);
            url = image.OriginalUrl;
            using var http = new HttpClient();
            Assert.Equal(Encoding.ASCII.GetBytes("RIFFxxxxWEBP"), await http.GetByteArrayAsync(url));
            await service.DeleteAsync(fixture.Recipe.Id, image.ImageId, fixture.Owner.Id.ToString(), false, default);
        }
        // No Hangfire server running yet: metadata committed while object remains publicly readable.
        Assert.True(await fixture.Storage.ExistsAsync(url));
        await using (var db = fixture.NewDb())
        {
            var file = await db.StoredFiles.SingleAsync();
            storedId = file.Id;
            Assert.NotNull(file.DeletionRequestedAt);
            Assert.NotNull(file.DeletionJobId);
            Assert.Null(file.DeletedAt);
            Assert.Empty(await db.RecipeImages.ToListAsync());
            Assert.Null((await db.Recipes.SingleAsync()).ImageUrl);
            using var scope = fixture.Services.CreateScope();
            Assert.Equal(file.DeletionJobId, scope.ServiceProvider.GetRequiredService<IFileDeletionQueue>().Enqueue(file.Id));
        }
        await fixture.RunWorkerUntilDeletedAsync(1);
        Assert.False(await fixture.Storage.ExistsAsync(url));
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeleteStoredFileJob>().ExecuteAsync(storedId, default);

        // Simulate a process stopping after committing the deletion intent but before enqueue.
        var pending = new StoredFile { OwnerId = fixture.Owner.Id, Url = url, Status = StoredFileStatus.DeletePending, DeletionRequestedAt = DateTime.UtcNow };
        await using (var db = fixture.NewDb())
        {
            // Reuse a valid trusted URL with a new object key; deleting an absent object must succeed.
            pending.Url = url.Replace(".webp", "-absent.webp");
            var reference = StorageTestExtensions.ReferenceFromFixtureUrl(pending.Url);
            pending.BucketName = reference.BucketName;
            pending.ObjectKey = reference.ObjectKey;
            db.StoredFiles.Add(pending);
            await db.SaveChangesAsync();
        }
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
        await fixture.RunWorkerUntilDeletedAsync(2);
    }

    [RecipeLiveFact]
    public async Task FailedStorageDeletionRetriesThreeTimesAndReconciliationPreservesBudget()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var pending = new StoredFile { OwnerId = fixture.Owner.Id, Url = "http://unused.test/file.webp", BucketName = "test", ObjectKey = "file.webp", Status = StoredFileStatus.DeletePending, DeletionRequestedAt = DateTime.UtcNow };
        await using (var db = fixture.NewDb())
        {
            db.StoredFiles.Add(pending);
            await db.SaveChangesAsync();
        }
        fixture.Faults.FailDelete = true;
        string jobId;
        using (var scope = fixture.Services.CreateScope())
            jobId = scope.ServiceProvider.GetRequiredService<IFileDeletionQueue>().Enqueue(pending.Id);
        using var server = fixture.CreateWorker();
        using var connection = fixture.Services.GetRequiredService<JobStorage>().GetConnection();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var client = fixture.Services.GetRequiredService<IBackgroundJobClient>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            while (connection.GetStateData(jobId)?.Name != "Scheduled") await Task.Delay(50, timeout.Token);
            Assert.Equal(attempt.ToString(), connection.GetJobParameter(jobId, "RetryCount"));
            // Advance scheduled retry immediately; production retains its 60/300/1800-second delays.
            Assert.True(client.Requeue(jobId, "Scheduled"));
        }
        while (connection.GetStateData(jobId)?.Name != "Failed") await Task.Delay(50, timeout.Token);
        Assert.Equal(4, fixture.Faults.DeleteCalls); // initial attempt plus three retries
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FileDeletionReconciliationJob>().ExecuteAsync(default);
            Assert.Equal(jobId, scope.ServiceProvider.GetRequiredService<IFileDeletionQueue>().Enqueue(pending.Id));
        }
        await using var check = fixture.NewDb();
        Assert.Null((await check.StoredFiles.SingleAsync()).DeletedAt);
        Assert.Equal("Failed", connection.GetStateData(jobId)?.Name);
    }

    private static IFormFile Image()
    {
        var bytes = Encoding.ASCII.GetBytes("RIFFxxxxWEBP");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "food.webp")
        { Headers = new HeaderDictionary(), ContentType = "image/webp" };
    }

    private sealed class RejectNewImageInterceptor : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<RecipeImage>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Injected final-save failure");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ObservedStorage(IFileStorageService inner) : IFileStorageService
    {
        public StorageObjectReference CreateReference(string folder, string fileName) => inner.CreateReference(folder, fileName);
        public string GetPublicUrl(StorageObjectReference reference) => inner.GetPublicUrl(reference);
        public string? UploadedUrl;
        public async Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken cancellationToken = default)
        {
            await inner.UploadAsync(file, reference, cancellationToken);
            UploadedUrl = inner.GetPublicUrl(reference);
        }
        public Task DeleteAsync(StorageObjectReference reference, CancellationToken cancellationToken = default) => inner.DeleteAsync(reference, cancellationToken);
        public Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken cancellationToken = default) => inner.ExistsAsync(reference, cancellationToken);
        public Task UploadStreamAsync(Stream stream, string fileName, string contentType, StorageObjectReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _connection;
        private readonly string _bucket = "recipe-test-" + Guid.NewGuid().ToString("N");
        private readonly IMinioClient _minio;
        public ServiceProvider Services { get; private set; } = null!;
        public MinioFileStorageService Storage { get; }
        public FaultableStorage Faults { get; }
        public MinioOptions OptionsConfig { get; }
        public RejectNewImageInterceptor SaveFailure { get; } = new();
        public User Owner { get; } = new() { Id = Guid.NewGuid(), UserName = "image-owner", Email = "owner@example.test" };
        public Recipe Recipe { get; } = new() { Title = "Test", Slug = "test", Category = new Category { Name = "Test", Slug = "test" } };

        public Fixture()
        {
            _connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CULINARY_TEST_POSTGRES") ??
                "Host=localhost;Port=5432;Username=postgres;Password=postgres")
            { Database = "culinary_image_test_" + Guid.NewGuid().ToString("N") }.ConnectionString;
            var options = new MinioOptions
            {
                Endpoint = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ENDPOINT") ?? "localhost:9000",
                AccessKey = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ACCESS_KEY") ?? "minioadmin",
                SecretKey = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_SECRET_KEY") ?? "minioadmin",
                BucketName = _bucket
            };
            OptionsConfig = options;
            _minio = new MinioClient().WithEndpoint(options.Endpoint).WithCredentials(options.AccessKey, options.SecretKey).Build();
            Storage = new MinioFileStorageService(_minio, Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<MinioFileStorageService>.Instance);
            Faults = new FaultableStorage(Storage);
        }
        public ApplicationDbContext NewDb(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connection).AddInterceptors(interceptors).Options);
        public StorageMaintenanceService Maintenance(ApplicationDbContext db) => new(db, _minio, Options.Create(OptionsConfig), Storage);
        public Dictionary<string, string> ApiEnvironment() => new()
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            // Restricted Windows test accounts cannot write to the machine Event Log.
            ["Logging__EventLog__LogLevel__Default"] = "None",
            ["Cache__InstancePrefix"] = _bucket + ":",
            ["ConnectionStrings__DefaultConnection"] = _connection,
            ["ConnectionStrings__Redis"] = Environment.GetEnvironmentVariable("CULINARY_TEST_REDIS") ?? "localhost:6379",
            ["MinIO__Endpoint"] = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ENDPOINT") ?? "localhost:9000",
            ["MinIO__AccessKey"] = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_ACCESS_KEY") ?? "minioadmin",
            ["MinIO__SecretKey"] = Environment.GetEnvironmentVariable("CULINARY_TEST_MINIO_SECRET_KEY") ?? "minioadmin",
            ["MinIO__BucketName"] = _bucket,
            ["Jwt__Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ["Hangfire__DashboardEnabled"] = "true"
        };
        public async Task InitializeAsync()
        {
            await using var db = NewDb();
            await db.Database.MigrateAsync();
            Recipe.AuthorId = Owner.Id.ToString();
            db.Users.Add(Owner);
            db.Recipes.Add(Recipe);
            await db.SaveChangesAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(_connection).AddInterceptors(SaveFailure));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddSingleton<IFileStorageService>(Faults);
            services.AddScoped<IRecipeImageTransactionFactory, RecipeImageTransactionFactory>();
            services.AddScoped<IFileLifecycleSessionFactory, FileLifecycleSessionFactory>();
            services.AddScoped<IFileDeletionQueue, HangfireFileDeletionQueue>();
            services.AddScoped<RecipeImageService>();
            services.AddScoped<DeleteStoredFileJob>();
            services.AddScoped<FileDeletionReconciliationJob>();
            services.AddHangfire(config => config.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(_connection),
                new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromMilliseconds(100) }));
            Services = services.BuildServiceProvider();
        }
        public BackgroundJobServer CreateWorker() => new(new BackgroundJobServerOptions
            {
                WorkerCount = 2,
                Activator = new ScopeActivator(Services.GetRequiredService<IServiceScopeFactory>())
            }, Services.GetRequiredService<JobStorage>());
        public async Task RunWorkerUntilDeletedAsync(int expected)
        {
            using var server = CreateWorker();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                await using var db = NewDb();
                if (await db.StoredFiles.CountAsync(x => x.DeletedAt != null, timeout.Token) == expected) break;
                await Task.Delay(100, timeout.Token);
            }
        }
        public async ValueTask DisposeAsync()
        {
            if (Services != null) await Services.DisposeAsync();
            try
            {
                if (await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket)))
                {
                    // Only this fixture's generated bucket is cleaned, including objects left by failed assertions.
                    await foreach (var item in _minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(_bucket).WithRecursive(true)))
                        await _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(item.Key));
                    await _minio.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(_bucket));
                }
            }
            finally
            {
                Storage.Dispose();
                _minio.Dispose();
                await using var db = NewDb();
                await db.Database.EnsureDeletedAsync();
            }
        }
    }
    private sealed class FaultableStorage(IFileStorageService inner) : IFileStorageService
    {
        public StorageObjectReference CreateReference(string folder, string fileName) => inner.CreateReference(folder, fileName);
        public string GetPublicUrl(StorageObjectReference reference) => inner.GetPublicUrl(reference);
        public bool FailDelete;
        public bool FailAfterUpload;
        public int DeleteCalls;
        public async Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken cancellationToken = default)
        {
            await inner.UploadAsync(file, reference, cancellationToken);
            if (FailAfterUpload) throw new HttpRequestException("PUT succeeded but response was lost");
        }
        public Task DeleteAsync(StorageObjectReference reference, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref DeleteCalls);
            if (FailDelete) throw new HttpRequestException("Injected MinIO connection failure");
            return inner.DeleteAsync(reference, cancellationToken);
        }
        public Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken cancellationToken = default) => inner.ExistsAsync(reference, cancellationToken);
        public Task UploadStreamAsync(Stream stream, string fileName, string contentType, StorageObjectReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class ScopeActivator(IServiceScopeFactory scopes) : JobActivator
    {
        public override JobActivatorScope BeginScope(JobActivatorContext context) => new Scope(scopes.CreateScope());
        private sealed class Scope(IServiceScope scope) : JobActivatorScope
        {
            public override object Resolve(Type type) => scope.ServiceProvider.GetRequiredService(type);
            public override void DisposeScope() => scope.Dispose();
        }
    }
    private sealed class RecipeLiveFactAttribute : FactAttribute
    {
        public RecipeLiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("CULINARY_LIVE_TESTS") != "1")
                Skip = "Set CULINARY_LIVE_TESTS=1 with PostgreSQL and MinIO running.";
        }
    }
}
