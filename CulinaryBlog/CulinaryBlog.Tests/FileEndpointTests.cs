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
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class FileEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly FakeStorage _storage = new();
    private readonly User _owner = new() { Id = Guid.NewGuid(), UserName = "owner" };
    private readonly User _other = new() { Id = Guid.NewGuid(), UserName = "other" };
    private JwtService _jwt = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        const string key = "file-tests-only-signing-key-with-at-least-32-bytes";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key });
        _jwt = new JwtService(builder.Configuration);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            });
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddSingleton<IFileStorageService>(_storage);
        builder.Services.AddFileEndpoints();
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.UseFileRequestLimits();
        _app.MapFileEndpoints();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(_owner, _other);
            await db.SaveChangesAsync();
        }
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    [Fact]
    public async Task GuestsCannotUploadDeleteOrProbe()
    {
        using var form = ImageForm();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("/api/v1/files/upload", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync("/api/v1/files/?fileUrl=x")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/files/exists?fileUrl=x")).StatusCode);
        Assert.Equal(0, _storage.UploadCalls);
        Assert.Equal(0, _storage.DeleteCalls);
    }

    [Fact]
    public async Task OnlyOwnerCanDeleteAndRepeatDeleteIsIdempotent()
    {
        SignIn(_owner);
        var file = await Upload();
        var query = "?fileUrl=" + Uri.EscapeDataString(file.Url);
        Assert.Contains($"users/{_owner.Id:N}/uploads/", file.Url);
        SignIn(_other);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.DeleteAsync("/api/v1/files/" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/files/exists" + query)).StatusCode);
        Assert.Equal(0, _storage.DeleteCalls);
        Assert.True(_storage.Objects.ContainsKey(file.Url));
        SignIn(_owner);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/v1/files/" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/v1/files/" + query)).StatusCode);
        Assert.Equal(1, _storage.DeleteCalls);
        Assert.False(_storage.Objects.ContainsKey(file.Url));
    }

    [Fact]
    public async Task UnknownOrLegacyFilesAreNeverSentToStorage()
    {
        SignIn(_owner);
        var response = await _client.DeleteAsync("/api/v1/files/?fileUrl=" + Uri.EscapeDataString("http://localhost:9000/culinary-blog/legacy.png"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _storage.DeleteCalls);
    }

    [Fact]
    public async Task StorageFailureReturns503AndCanBeRetried()
    {
        SignIn(_owner);
        var file = await Upload();
        _storage.Fail = true;
        var path = "/api/v1/files/?fileUrl=" + Uri.EscapeDataString(file.Url);
        var failed = await _client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain("private-storage-detail", await failed.Content.ReadAsStringAsync());
        Assert.True(_storage.Objects.ContainsKey(file.Url));
        _storage.Fail = false;
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(path)).StatusCode);
    }

    [Fact]
    public async Task UploadIsRateLimitedPerUser()
    {
        SignIn(_owner);
        for (var i = 0; i < 10; i++) await Upload();
        using var form = ImageForm();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.PostAsync("/api/v1/files/upload", form)).StatusCode);
        SignIn(_other);
        await Upload();
        Assert.Equal(11, _storage.UploadCalls);
    }

    [Fact]
    public async Task InvalidImageOrFolderReturns400()
    {
        SignIn(_owner);
        using var badImage = ImageForm("RIFGxxxxWEBP");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/v1/files/upload", badImage)).StatusCode);
        using var badFolder = ImageForm();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/v1/files/upload?folder=..%2Fescape", badFolder)).StatusCode);
        Assert.Empty(_storage.Objects);
    }

    private void SignIn(User user) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwt.GenerateAccessToken(user));

    private async Task<UploadFileResultDto> Upload()
    {
        using var form = ImageForm();
        var response = await _client.PostAsync("/api/v1/files/upload", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadFileResultDto>())!;
    }

    private static MultipartFormDataContent ImageForm(string bytes = "RIFFxxxxWEBP")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(bytes));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
        form.Add(content, "file", "test.webp");
        return form;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
    }

    private sealed class FakeStorage : IFileStorageService
    {
        public ConcurrentDictionary<string, byte> Objects { get; } = new();
        public int UploadCalls;
        public int DeleteCalls;
        public bool Fail;
        public async Task<string> UploadAsync(IFormFile file, string folder = "uploads", CancellationToken cancellationToken = default)
        {
            await FileValidationHelper.ValidateImageFileAsync(file, cancellationToken);
            if (Fail) throw new HttpRequestException("private-storage-detail");
            UploadCalls++;
            var url = $"http://localhost:9000/culinary-blog/{folder}/{Guid.NewGuid():N}.webp";
            Objects[url] = 0;
            return url;
        }
        public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new HttpRequestException("private-storage-detail");
            DeleteCalls++;
            Objects.TryRemove(url, out _);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(Objects.ContainsKey(url));
        public Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, string folder = "uploads", CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
