using System.Net;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleLoginCorsHttpTests
{
    [Theory]
    [InlineData("https://frontend.example", "https://other.example")]
    [InlineData("https://another-frontend.example", "https://frontend.example")]
    public async Task Google_login_preflight_allows_only_configured_origin(
        string allowedOrigin, string disallowedOrigin)
    {
        await using var app = await StartAppAsync(allowedOrigin);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        using var allowed = await PreflightAsync(client, allowedOrigin);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(allowedOrigin, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.DoesNotContain("Access-Control-Allow-Credentials", allowed.Headers.Select(header => header.Key));

        using var disallowed = await PreflightAsync(client, disallowedOrigin);
        Assert.False(disallowed.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.DoesNotContain("*", allowed.Headers.SelectMany(header => header.Value));
        Assert.DoesNotContain("*", disallowed.Headers.SelectMany(header => header.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("https://frontend.example/path")]
    public void Missing_or_invalid_origin_configuration_fails_closed(string? origin)
    {
        var values = origin is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = origin };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddFrontendCors(configuration));
    }

    private static async Task<WebApplication> StartAppAsync(string allowedOrigin)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = allowedOrigin
        });
        builder.Services.AddSingleton(Mock.Of<IMediator>());
        builder.Services.AddFrontendCors(builder.Configuration);

        var app = builder.Build();
        app.UseCors(FrontendCorsConfiguration.PolicyName);
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/google");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        return client.SendAsync(request);
    }
}
