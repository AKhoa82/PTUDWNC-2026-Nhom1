using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RefreshTokenPostgresTests
{
    [PostgresFact]
    public async Task Refresh_rotates_token_and_rejects_reuse()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "refresh_test_" + Guid.NewGuid().ToString("N");
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
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["Jwt:Key"] = new string('k', 64);
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
            builder.Services.AddScoped<IApplicationDbContext>(services =>
                services.GetRequiredService<ApplicationDbContext>());
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddSingleton(Mock.Of<IMediator>());
            builder.Services.AddIdentityCore<User>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var app = builder.Build();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await context.Database.MigrateAsync();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = new User { UserName = "refresh-author", Email = "refresh@example.com", FullName = "Refresh Author" };
                Assert.True((await userManager.CreateAsync(user, "Valid1!Pass")).Succeeded);

                var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                context.RefreshTokens.Add(new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TokenHash = Hash(rawToken),
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                });
                await context.SaveChangesAsync();

                app.MapAuthEndpoints();
                await app.StartAsync();
                using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

                using var first = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(rawToken));
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
                var response = await first.Content.ReadFromJsonAsync<AuthResponseDto>();
                Assert.NotNull(response);
                Assert.NotEqual(rawToken, response.RefreshToken);

                context.ChangeTracker.Clear();
                var oldToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == Hash(rawToken));
                Assert.NotNull(oldToken.RevokedAt);
                Assert.Equal(Hash(response.RefreshToken), oldToken.ReplacedByTokenHash);

                using var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(rawToken));
                Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

                using var second = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(response.RefreshToken));
                Assert.Equal(HttpStatusCode.OK, second.StatusCode);
                Assert.Equal(3, await context.RefreshTokens.CountAsync());
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
