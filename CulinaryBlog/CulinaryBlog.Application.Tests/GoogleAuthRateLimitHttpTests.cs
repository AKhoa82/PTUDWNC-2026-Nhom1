using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Infrastructure;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleAuthRateLimitHttpTests
{
    [RedisFact]
    public async Task Redis_quota_is_shared_by_two_independent_api_instances()
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTH_TEST_REDIS")!;
        using var redisA = await ConnectionMultiplexer.ConnectAsync(connectionString);
        using var redisB = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var clientIp = new IPAddress(Guid.NewGuid().ToByteArray()).ToString();
        await using var appA = await StartAppAsync(RejectTestToken().Object, new RedisAuthRateLimitCounter(redisA));
        await using var appB = await StartAppAsync(RejectTestToken().Object, new RedisAuthRateLimitCounter(redisB));
        using var clientA = CreateClient(appA, clientIp);
        using var clientB = CreateClient(appB, clientIp);

        for (var request = 0; request < 5; request++)
            await AssertRequestReachedEndpointAsync(clientA);
        for (var request = 0; request < 5; request++)
            await AssertRequestReachedEndpointAsync(clientB);

        using var rejected = await PostGoogleAsync(clientB);
        await AssertRateLimitedAsync(rejected);
    }

    [Fact]
    public async Task Google_login_quota_is_shared_by_multiple_api_instances()
    {
        var counter = new SharedFakeRateLimitCounter();
        var mediatorA = RejectTestToken();
        var mediatorB = RejectTestToken();
        await using var appA = await StartAppAsync(mediatorA.Object, counter);
        await using var appB = await StartAppAsync(mediatorB.Object, counter);
        using var clientA = CreateClient(appA, "192.0.2.10");
        using var clientB = CreateClient(appB, "192.0.2.10");

        for (var request = 0; request < 5; request++)
            await AssertRequestReachedEndpointAsync(clientA);
        for (var request = 0; request < 5; request++)
            await AssertRequestReachedEndpointAsync(clientB);

        using var rejected = await PostGoogleAsync(clientA);
        await AssertRateLimitedAsync(rejected);
        mediatorA.Verify(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        mediatorB.Verify(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task Google_login_quota_is_separate_for_each_client_ip()
    {
        var counter = new SharedFakeRateLimitCounter();
        var mediator = RejectTestToken();
        await using var app = await StartAppAsync(mediator.Object, counter);
        using var clientA = CreateClient(app, "192.0.2.10");
        using var clientB = CreateClient(app, "192.0.2.11");

        for (var request = 0; request < 10; request++)
            await AssertRequestReachedEndpointAsync(clientA);
        await AssertRequestReachedEndpointAsync(clientB);

        using var rejectedA = await PostGoogleAsync(clientA);
        await AssertRateLimitedAsync(rejectedA);
        mediator.Verify(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(11));
    }

    private static Mock<IMediator> RejectTestToken()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid test token."));
        return mediator;
    }

    private static async Task<WebApplication> StartAppAsync(IMediator mediator, IAuthRateLimitCounter counter)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(mediator);
        builder.Services.AddSingleton<IAuthRateLimitCounter>(counter);
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            // Test-only simulation of the IP after trusted forwarded-header processing.
            if (context.Request.Headers.TryGetValue("X-Test-Client-IP", out var address))
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            await next(context);
        });
        app.UseMiddleware<DistributedAuthRateLimitMiddleware>();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app, string ipAddress)
    {
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", ipAddress);
        return client;
    }

    private static Task<HttpResponseMessage> PostGoogleAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "invalid-test-token" });

    private static async Task AssertRequestReachedEndpointAsync(HttpClient client)
    {
        using var response = await PostGoogleAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid test token.", problem.RootElement.GetProperty("detail").GetString());
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var seconds) && seconds > 0);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Too Many Requests", problem.RootElement.GetProperty("title").GetString());
    }

    private sealed class SharedFakeRateLimitCounter : IAuthRateLimitCounter
    {
        private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

        public Task<RateLimitCounterResult> IncrementAsync(
            string key,
            TimeSpan window,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            var bucket = _buckets.GetOrAdd(key, _ => new Bucket(now));
            lock (bucket)
            {
                if (now - bucket.StartedAt >= window)
                {
                    bucket.StartedAt = now;
                    bucket.Count = 0;
                }
                bucket.Count++;
                return Task.FromResult(new RateLimitCounterResult(bucket.Count, window - (now - bucket.StartedAt)));
            }
        }

        private sealed class Bucket(DateTimeOffset startedAt)
        {
            public DateTimeOffset StartedAt { get; set; } = startedAt;
            public long Count { get; set; }
        }
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTH_TEST_REDIS")))
            Skip = "Set AUTH_TEST_REDIS to run Redis-backed multi-instance rate-limit integration tests.";
    }
}
