using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeImageEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly TestImageStorage _storage = new();
    private readonly TestDeletionQueue _queue = new();
    private readonly User _owner = new() { Id = Guid.NewGuid(), UserName = "owner" };
    private readonly User _other = new() { Id = Guid.NewGuid(), UserName = "other" };
    private readonly Recipe _recipe = new() { Title = "Recipe", Slug = "recipe" };
    private JwtService _jwt = null!;
    private string Path => $"/api/v1/recipes/{_recipe.Id}/images";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        const string key = "recipe-image-tests-only-key-at-least-32-bytes";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key });
        _jwt = new JwtService(builder.Configuration);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            });
        builder.Services.AddAuthorization();
        builder.Services.AddOutputCache();
        builder.Services.AddDistributedMemoryCache();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddSingleton<IFileStorageService>(_storage);
        builder.Services.AddSingleton<IFileDeletionQueue>(_queue);
        builder.Services.AddSingleton<IRecipeImageTransactionFactory, TestImageTransactionFactory>();
        builder.Services.AddSingleton<IFileLifecycleSessionFactory, TestLifecycleSessionFactory>();
        builder.Services.AddRecipeImageFeature();
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.UseRecipeImageRequestLimits();
        _app.MapRecipeImageEndpoints();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _recipe.AuthorId = _owner.Id.ToString();
            db.Users.AddRange(_owner, _other);
            db.Recipes.Add(_recipe);
            await db.SaveChangesAsync();
        }
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    [Fact]
    public async Task GuestsAreRejectedAndGenericFileRoutesAreAbsent()
    {
        using var form = ImageForm();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync(Path, form)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PatchAsJsonAsync(Path + "/" + Guid.NewGuid(), new { isPrimary = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync(Path + "/" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/v1/files/upload", ImageForm())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/v1/files?fileUrl=x")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/files/exists?fileUrl=x")).StatusCode);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task NonOwnerCannotMutateButAdminCan()
    {
        SignIn(_other);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(Path, ImageForm())).StatusCode);
        SignIn(_owner);
        var image = await Upload();
        SignIn(_other);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PatchAsJsonAsync(Path + "/" + image.ImageId, new { isPrimary = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.DeleteAsync(Path + "/" + image.ImageId)).StatusCode);
        SignIn(_other, "Admin");
        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync(Path + "/" + image.ImageId, new { altText = "Admin edit" })).StatusCode);
        await Upload();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(Path + "/" + image.ImageId)).StatusCode);
    }

    [Fact]
    public async Task GalleryPromotesReplacesAndClearsPrimaryWithoutDeletingStorageInline()
    {
        SignIn(_owner);
        var first = await Upload();
        var second = await Upload();
        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        Assert.Equal(1, second.OrderIndex);
        Assert.Null(first.MediumUrl);
        Assert.Null(first.ThumbnailUrl);
        Assert.Contains($"recipes/{_recipe.Id:N}/", first.OriginalUrl);
        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync(Path + "/" + second.ImageId, new { isPrimary = true, altText = " Food ", orderIndex = 0 })).StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(second.OriginalUrl, (await db.Recipes.SingleAsync()).ImageUrl);
            Assert.Single(await db.RecipeImages.Where(x => x.IsPrimary).ToListAsync());
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(Path + "/" + second.ImageId)).StatusCode);
        Assert.True(_storage.Objects.ContainsKey(second.OriginalUrl));
        Assert.Single(_queue.Ids);
        await WithDb(async db =>
        {
            Assert.Equal(first.OriginalUrl, (await db.Recipes.SingleAsync()).ImageUrl);
            var stored = await db.StoredFiles.SingleAsync(x => x.Url == second.OriginalUrl);
            Assert.NotNull(stored.DeletionRequestedAt);
            Assert.Null(stored.DeletedAt);
            var job = new DeleteStoredFileJob(db, _storage, NullLogger<DeleteStoredFileJob>.Instance);
            await job.ExecuteAsync(stored.Id, default);
            await job.ExecuteAsync(stored.Id, default);
            Assert.NotNull(stored.DeletedAt);
        });
        Assert.Equal(1, _storage.DeleteCalls);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(Path + "/" + first.ImageId)).StatusCode);
        await WithDb(async db => Assert.Null((await db.Recipes.SingleAsync()).ImageUrl));
        Assert.NotNull(await _app.Services.GetRequiredService<IDistributedCache>().GetStringAsync(GetRecipesQueryHandler.CacheVersionKey));
    }

    [Fact]
    public async Task ExplicitPrimaryUploadDemotesOldPrimary()
    {
        SignIn(_owner);
        await Upload();
        using var form = ImageForm();
        form.Add(new StringContent("true"), "isPrimary");
        var response = await _client.PostAsync(Path, form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.True(image.IsPrimary);
        await WithDb(async db =>
        {
            Assert.Single(await db.RecipeImages.Where(x => x.IsPrimary).ToListAsync());
            Assert.Equal(image.OriginalUrl, (await db.Recipes.SingleAsync()).ImageUrl);
        });
    }

    [Fact]
    public async Task PatchDistinguishesOmittedAltTextFromExplicitNullAndValidatesMetadata()
    {
        SignIn(_owner);
        var image = await Upload();
        var path = Path + "/" + image.ImageId;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PatchAsJsonAsync(path, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PatchAsJsonAsync(path, new { orderIndex = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PatchAsJsonAsync(path, new { isPrimary = false })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PatchAsJsonAsync(path, new { altText = new string('x', 201) })).StatusCode);
        await _client.PatchAsJsonAsync(path, new { altText = "Food" });
        var unchanged = await _client.PatchAsJsonAsync(path, new { orderIndex = 3 });
        Assert.Equal("Food", (await unchanged.Content.ReadFromJsonAsync<RecipeImageDto>())!.AltText);
        var cleared = await _client.PatchAsJsonAsync(path, new { altText = (string?)null });
        Assert.Null((await cleared.Content.ReadFromJsonAsync<RecipeImageDto>())!.AltText);
    }

    [Fact]
    public async Task ForeignRecipeImageReturns404()
    {
        SignIn(_owner);
        var image = await Upload();
        var otherRecipe = new Recipe { Slug = "other", AuthorId = _owner.Id.ToString() };
        await WithDb(async db => { db.Recipes.Add(otherRecipe); await db.SaveChangesAsync(); });
        var path = $"/api/v1/recipes/{otherRecipe.Id}/images/{image.ImageId}";
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync(path, new { isPrimary = true })).StatusCode);
        Assert.Empty(_queue.Ids);
    }

    [Fact]
    public async Task LegacyDeleteDoesNotReachMinioOrQueue()
    {
        var legacy = new RecipeImage { RecipeId = _recipe.Id, OriginalUrl = "https://example.test/legacy.png", IsPrimary = true };
        await WithDb(async db => { db.RecipeImages.Add(legacy); (await db.Recipes.SingleAsync()).ImageUrl = legacy.OriginalUrl; await db.SaveChangesAsync(); });
        SignIn(_owner);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(Path + "/" + legacy.Id)).StatusCode);
        Assert.Empty(_queue.Ids);
        Assert.Equal(0, _storage.DeleteCalls);
    }

    [Fact]
    public async Task EnqueueFailureLeavesPendingDeletionForReconciliation()
    {
        SignIn(_owner);
        var image = await Upload();
        _queue.Fail = true;
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(Path + "/" + image.ImageId)).StatusCode);
        await WithDb(async db =>
        {
            var file = await db.StoredFiles.SingleAsync();
            Assert.NotNull(file.DeletionRequestedAt);
            Assert.Null(file.DeletionJobId);
            _queue.Fail = false;
            await new FileDeletionReconciliationJob(db, _queue, NullLogger<FileDeletionReconciliationJob>.Instance).ExecuteAsync(default);
            Assert.Contains(file.Id, _queue.Ids);
            _storage.Fail = true;
            await Assert.ThrowsAsync<HttpRequestException>(() => new DeleteStoredFileJob(db, _storage, NullLogger<DeleteStoredFileJob>.Instance).ExecuteAsync(file.Id, default));
            Assert.Null(file.DeletedAt);
        });
    }

    [Fact]
    public async Task ExactlyFiveMiBWithMultipartOverheadSucceedsAndOneExtraByteFails()
    {
        SignIn(_owner);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync(Path, ImageForm(size: 5 * 1024 * 1024))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync(Path, ImageForm(size: 5 * 1024 * 1024 + 1))).StatusCode);
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task UploadLimitIsSharedByUsersAtSameIpWithRetryAfter()
    {
        SignIn(_owner);
        for (var i = 0; i < 5; i++) await Upload();
        SignIn(_other, "Admin");
        var response = await _client.PostAsync(Path, ImageForm());
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(5, _storage.Objects.Count);
    }

    [Fact]
    public async Task InvalidMagicReturns400AndStorageFailureReturnsSanitized503()
    {
        SignIn(_owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync(Path, ImageForm("RIFGxxxxWEBP"))).StatusCode);
        _storage.Fail = true;
        var response = await _client.PostAsync(Path, ImageForm());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("private-storage-detail", await response.Content.ReadAsStringAsync());
        Assert.Empty(_storage.Objects);
    }

    private void SignIn(User user, params string[] roles) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwt.GenerateAccessToken(user, roles));
    private async Task<RecipeImageDto> Upload()
    {
        using var form = ImageForm();
        var response = await _client.PostAsync(Path, form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
    }
    private async Task WithDb(Func<ApplicationDbContext, Task> action)
    {
        using var scope = _app.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
    internal static MultipartFormDataContent ImageForm(string magic = "RIFFxxxxWEBP", int size = 12)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes(magic).CopyTo(bytes, 0);
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
        form.Add(content, "file", "test.webp");
        return form;
    }
    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
    }
}

internal sealed class TestImageStorage : IFileStorageService
{
    public StorageObjectReference CreateReference(string folder, string fileName) => new("culinary-blog", $"{folder}/{Guid.NewGuid():N}.webp");
    public string GetPublicUrl(StorageObjectReference reference) => $"http://localhost:9000/{reference.BucketName}/{reference.ObjectKey}";
    public ConcurrentDictionary<string, byte> Objects { get; } = new();
    public int DeleteCalls;
    public bool Fail;
    public async Task UploadAsync(IFormFile file, StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        await FileValidationHelper.ValidateImageFileAsync(file, cancellationToken);
        if (Fail) throw new HttpRequestException("private-storage-detail");
        var url = GetPublicUrl(reference);
        Objects[url] = 0;
    }
    public Task DeleteAsync(StorageObjectReference reference, CancellationToken cancellationToken = default)
    {
        if (Fail) throw new HttpRequestException("private-storage-detail");
        Interlocked.Increment(ref DeleteCalls);
        Objects.TryRemove(GetPublicUrl(reference), out _);
        return Task.CompletedTask;
    }
    public Task<bool> ExistsAsync(StorageObjectReference reference, CancellationToken cancellationToken = default) => Task.FromResult(Objects.ContainsKey(GetPublicUrl(reference)));
    public Task UploadStreamAsync(Stream stream, string fileName, string contentType, StorageObjectReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class TestDeletionQueue : IFileDeletionQueue
{
    public List<Guid> Ids { get; } = [];
    public bool Fail;
    public string Enqueue(Guid id)
    {
        if (Fail) throw new InvalidOperationException("queue unavailable");
        Ids.Add(id);
        return Ids.Count.ToString();
    }
}

// HTTP tests isolate transport/business behavior; PostgreSQL tests cover actual transactions/locks.
internal sealed class TestImageTransactionFactory : IRecipeImageTransactionFactory, IRecipeImageTransaction
{
    public Task<IRecipeImageTransaction> BeginAsync(Guid recipeId, CancellationToken ct) => Task.FromResult<IRecipeImageTransaction>(this);
    public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestLifecycleSessionFactory(IServiceScopeFactory scopes) : IFileLifecycleSessionFactory
{
    public IFileLifecycleSession Create() => new Session(scopes.CreateAsyncScope());
    private sealed class Session(AsyncServiceScope scope) : IFileLifecycleSession
    {
        public IApplicationDbContext Db { get; } = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public Task<IRecipeImageTransaction> BeginAsync(Guid? recipeId, Guid fileId, CancellationToken ct) =>
            Task.FromResult<IRecipeImageTransaction>(new TestImageTransactionFactory());
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
}
