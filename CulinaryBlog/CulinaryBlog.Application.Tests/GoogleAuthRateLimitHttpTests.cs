using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleAuthRateLimitHttpTests
{
    [Fact]
    public async Task Eleventh_google_login_request_from_same_ip_returns_429_with_retry_after()
    {
        var mediator = RejectTestToken();
        await using var app = await StartAppAsync(mediator.Object);
        using var client = CreateClient(app, "192.0.2.10");

        for (var request = 0; request < 10; request++)
            await AssertRequestReachedEndpointAsync(client);

        using var rejected = await PostGoogleAsync(client);
        await AssertRateLimitedAsync(rejected);
        mediator.Verify(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task Google_login_rate_limit_has_independent_quota_for_each_ip()
    {
        var mediator = RejectTestToken();
        await using var app = await StartAppAsync(mediator.Object);
        using var clientA = CreateClient(app, "192.0.2.10");
        using var clientB = CreateClient(app, "192.0.2.11");

        for (var request = 0; request < 10; request++)
            await AssertRequestReachedEndpointAsync(clientA);

        await AssertRequestReachedEndpointAsync(clientB);
        using (var rejectedA = await PostGoogleAsync(clientA))
            await AssertRateLimitedAsync(rejectedA);

        for (var request = 1; request < 10; request++)
            await AssertRequestReachedEndpointAsync(clientB);

        using var rejectedB = await PostGoogleAsync(clientB);
        await AssertRateLimitedAsync(rejectedB);
        mediator.Verify(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(20));
    }

    private static Mock<IMediator> RejectTestToken()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid test token."));
        return mediator;
    }

    private static async Task<WebApplication> StartAppAsync(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(mediator);
        builder.Services.AddProblemDetails();
        builder.Services.AddRecipeImageFeature();
        builder.Services.AddRateLimiter(options =>
            options.AddPolicy<string, AuthRateLimitPolicy>(AuthRateLimitPolicy.Name));

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            // Test-only replacement for Kestrel's loopback address; production reads RemoteIpAddress directly.
            if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var address))
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            await next(context);
        });
        app.UseRateLimiter();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app, string ipAddress)
    {
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Test-Remote-IP", ipAddress);
        return client;
    }

    private static Task<HttpResponseMessage> PostGoogleAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "invalid-test-token" });

    private static async Task AssertRequestReachedEndpointAsync(HttpClient client)
    {
        using var response = await PostGoogleAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid test token.", problem.RootElement.GetProperty("detail").GetString());
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var seconds) && seconds > 0);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Too Many Requests", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("Quá giới hạn yêu cầu đăng nhập. Vui lòng thử lại sau.",
            problem.RootElement.GetProperty("detail").GetString());
    }
}
