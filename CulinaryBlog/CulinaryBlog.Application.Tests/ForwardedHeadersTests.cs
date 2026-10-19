using System.Net;
using CulinaryBlog.API.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class ForwardedHeadersTests
{
    [Fact]
    public async Task Forwarded_ip_from_configured_proxy_is_used_for_client_address()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client-ip");
        request.Headers.Add("X-Test-Immediate-Proxy", "192.0.2.10");
        request.Headers.Add("X-Forwarded-For", "198.51.100.99, 203.0.113.7");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("203.0.113.7", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Spoofed_forwarded_ip_from_untrusted_peer_is_ignored()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client-ip");
        request.Headers.Add("X-Test-Immediate-Proxy", "192.0.2.99");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("192.0.2.99", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Invalid_proxy_configuration_fails_closed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "0.0.0.0/0" }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTrustedForwardedHeaders(configuration));
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "192.0.2.10",
            ["ForwardedHeaders:ForwardLimit"] = "1"
        });
        builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Immediate-Proxy", out var address))
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            await next(context);
        });
        app.UseForwardedHeaders();
        app.MapGet("/client-ip", (HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString());
        await app.StartAsync();
        return app;
    }
}
