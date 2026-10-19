using System.Net;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleLoginOpenApiTests
{
    [Fact]
    public async Task Generated_document_describes_google_login_request_responses_and_anonymous_access()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Mock.Of<IMediator>());
        builder.Services.AddOpenApi();

        await using var app = builder.Build();
        app.MapOpenApi();
        app.MapAuthEndpoints();
        await app.StartAsync();

        var googleEndpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/api/v1/auth/google");
        Assert.NotNull(googleEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Null(googleEndpoint.Metadata.GetMetadata<IAuthorizeData>());

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty("/api/v1/auth/google").GetProperty("post");

        var request = operation.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var requestSchema = ResolveSchema(root, request);
        Assert.True(requestSchema.GetProperty("properties").TryGetProperty("idToken", out _));

        var responses = operation.GetProperty("responses");
        var success = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        var successSchema = ResolveSchema(root, success);
        Assert.True(successSchema.GetProperty("properties").TryGetProperty("accessToken", out _));
        Assert.True(successSchema.GetProperty("properties").TryGetProperty("refreshToken", out _));
        Assert.False(successSchema.GetProperty("properties").TryGetProperty("data", out _));

        foreach (var status in new[] { "400", "401", "422", "429", "500", "502" })
        {
            var error = responses.GetProperty(status);
            Assert.True(error.GetProperty("content").TryGetProperty("application/problem+json", out _));
        }

        Assert.True(!operation.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
            return schema;

        var name = reference.GetString()!.Split('/').Last();
        return root.GetProperty("components").GetProperty("schemas").GetProperty(name);
    }
}
