using System.Net.Http.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Register;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleLoginRefreshCookieTests
{
    [Theory]
    [InlineData("login")]
    [InlineData("register")]
    public async Task Production_email_auth_sets_refresh_cookie_without_exposing_token(string action)
    {
        const string refreshToken = "production-email-refresh-token";
        var authResponse = new AuthResponseDto(
            Guid.NewGuid(), "test-author", "Test Author", "author@example.com",
            "test-access-token", refreshToken,
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(7));
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);
        mediator.Setup(service => service.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);

        await using var app = await StartAppAsync(mediator.Object, Environments.Production);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var body = action == "login"
            ? new { email = "author@example.com", password = "Valid1!Pass" } as object
            : new { username = "test-author", email = "author@example.com", password = "Valid1!Pass",
                confirmPassword = "Valid1!Pass", fullName = "Test Author", termsAccepted = true };
        using var response = await client.PostAsJsonAsync($"/api/v1/auth/{action}", body);

        Assert.Equal(action == "login" ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.Created,
            response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{AuthEndpoints.RefreshTokenCookieName}={refreshToken}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(refreshToken, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_google_login_sets_http_only_cookie_and_does_not_return_refresh_token()
    {
        const string refreshToken = "test-refresh-token-that-must-not-reach-javascript";
        var authResponse = new AuthResponseDto(
            Guid.NewGuid(), "test-author", "Test Author", "author@example.com",
            "test-access-token", refreshToken,
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(7));
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);

        await using var app = await StartAppAsync(mediator.Object, Environments.Production);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/google", new { idToken = "test-google-id-token" });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers, header =>
            header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{AuthEndpoints.RefreshTokenCookieName}={refreshToken}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(refreshToken, body, StringComparison.Ordinal);
        Assert.Contains("accessToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Development_google_login_keeps_existing_response_contract_without_production_cookie()
    {
        const string refreshToken = "development-refresh-token";
        var authResponse = new AuthResponseDto(
            Guid.NewGuid(), "test-author", "Test Author", "author@example.com",
            "test-access-token", refreshToken,
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(7));
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);

        await using var app = await StartAppAsync(mediator.Object, Environments.Development);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/google", new { idToken = "test-google-id-token" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(refreshToken, body, StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static async Task<WebApplication> StartAppAsync(IMediator mediator, string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(mediator);

        var app = builder.Build();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }
}
