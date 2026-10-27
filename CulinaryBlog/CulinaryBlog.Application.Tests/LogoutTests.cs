using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class LogoutTests
{
    private const string JwtKey = "logout-test-key-with-at-least-32-bytes-1234567890";

    [Fact]
    public async Task Production_logout_revokes_cookie_token_and_deletes_cookie()
    {
        LogoutCommand? sent = null;
        await using var app = await StartAppAsync(Environments.Production, command => sent = command);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{AuthEndpoints.RefreshTokenCookieName}=cookie-token");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateJwt(Guid.NewGuid(), 15));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("cookie-token", sent?.RefreshToken);
        var deletion = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{AuthEndpoints.RefreshTokenCookieName}=", deletion);
        Assert.Contains("path=/api/v1/auth", deletion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Thu, 01 Jan 1970", deletion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valid_and_expired_bearer_tokens_revoke_only_their_owners_token()
    {
        LogoutCommand? sent = null;
        await using var app = await StartAppAsync(Environments.Development, command => sent = command);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var userId = Guid.NewGuid();

        foreach (var expiryMinutes in new[] { 15, -1 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
            {
                Content = JsonContent.Create(new LogoutRequest("body-token"))
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateJwt(userId, expiryMinutes));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(userId, sent?.UserId);
            Assert.Equal("body-token", sent?.RefreshToken);
        }
    }

    [Fact]
    public async Task Invalid_bearer_and_missing_refresh_token_are_rejected()
    {
        var sent = false;
        await using var app = await StartAppAsync(Environments.Development, _ => sent = true);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
        {
            Content = JsonContent.Create(new LogoutRequest("body-token"))
        };
        invalid.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-jwt");
        using var invalidResponse = await client.SendAsync(invalid);
        using var missingResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(null));
        using var noBearerResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest("body-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noBearerResponse.StatusCode);
        Assert.False(sent);
    }

    [Fact]
    public async Task Handler_is_idempotent_and_does_not_revoke_another_users_token()
    {
        await using var context = new RecipeAcceptanceContext();
        var ownerId = Guid.NewGuid();
        const string rawToken = "logout-handler-token";
        var token = new RefreshToken
        {
            UserId = ownerId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        var handler = new LogoutCommandHandler(context);

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), rawToken), CancellationToken.None);
        Assert.Null(token.RevokedAt);

        await handler.Handle(new LogoutCommand(ownerId, rawToken), CancellationToken.None);
        Assert.NotNull(token.RevokedAt);
        var revokedAt = token.RevokedAt;
        await handler.Handle(new LogoutCommand(ownerId, rawToken), CancellationToken.None);
        await handler.Handle(new LogoutCommand(ownerId, "unknown-token"), CancellationToken.None);
        Assert.Equal(revokedAt, token.RevokedAt);
    }

    private static async Task<WebApplication> StartAppAsync(string environmentName, Action<LogoutCommand> onLogout)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            });
        builder.Services.AddAuthorization();
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
            .Callback<LogoutCommand, CancellationToken>((command, _) => onLogout(command))
            .Returns(Task.CompletedTask);
        builder.Services.AddSingleton(mediator.Object);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static string CreateJwt(Guid userId, int expiryMinutes)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            notBefore: now.AddMinutes(-5),
            expires: now.AddMinutes(expiryMinutes),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
