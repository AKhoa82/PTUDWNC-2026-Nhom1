using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Infrastructure;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleLoginHttpProblemDetailsTests
{
    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Google_login_returns_problem_details_over_http(
        Exception failure, int expectedStatus, string expectedTitle, string expectedDetail)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        await using var app = await StartAppAsync(mediator.Object);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "test-token" });

        await AssertProblemAsync(response, expectedStatus, expectedTitle, expectedDetail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_google_id_token_is_rejected_by_mediatr_validation_pipeline(string? idToken)
    {
        var handler = new Mock<IRequestHandler<GoogleLoginCommand, AuthResponseDto>>();
        await using var app = await StartAppWithValidationPipelineAsync(handler.Object);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

        await AssertValidationProblemAsync(response);
        handler.Verify(service => service.Handle(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_google_id_token_is_rejected_by_mediatr_validation_pipeline()
    {
        var handler = new Mock<IRequestHandler<GoogleLoginCommand, AuthResponseDto>>();
        await using var app = await StartAppWithValidationPipelineAsync(handler.Object);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { });

        await AssertValidationProblemAsync(response);
        handler.Verify(service => service.Handle(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<Exception, int, string, string> FailureCases => new()
    {
        {
            new GoogleProfileException("Google profile does not contain an email address."),
            400, "Bad Request", "Google profile does not contain an email address."
        },
        {
            new UnauthorizedAccessException("Google ID token is invalid or expired.",
                new InvalidOperationException("SECRET_INTERNAL_EXCEPTION_MARKER")),
            401, "Unauthorized", "Google ID token is invalid or expired."
        },
        {
            new HttpRequestException("https://private.example/certs?secret=SECRET_INTERNAL_EXCEPTION_MARKER"),
            502, "Google verification unavailable",
            "Không thể liên hệ dịch vụ xác minh của Google. Vui lòng thử lại."
        },
        {
            new InvalidOperationException("SECRET_INTERNAL_EXCEPTION_MARKER stack trace"),
            500, "Internal Server Error", "Đã xảy ra lỗi hệ thống không mong muốn."
        }
    };

    private static Task<WebApplication> StartAppAsync(IMediator mediator) =>
        StartAppAsync(services => services.AddSingleton(mediator));

    private static Task<WebApplication> StartAppWithValidationPipelineAsync(
        IRequestHandler<GoogleLoginCommand, AuthResponseDto> handler) =>
        StartAppAsync(services =>
        {
            services.AddValidatorsFromAssembly(typeof(GoogleLoginCommandValidator).Assembly);
            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(GoogleLoginCommand).Assembly);
                configuration.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            });
            services.AddSingleton(handler);
        });

    private static async Task<WebApplication> StartAppAsync(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        configureServices(builder.Services);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(422, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var problem = json.RootElement;
        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal("Validation Failed", problem.GetProperty("title").GetString());
        Assert.Equal("https://tools.ietf.org/html/rfc4918#section-11.2", problem.GetProperty("type").GetString());
        Assert.Equal("Google ID token is required.",
            problem.GetProperty("errors").GetProperty("IdToken")[0].GetString());
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response, int expectedStatus, string expectedTitle, string expectedDetail)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var problem = json.RootElement;
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        var type = problem.GetProperty("type").GetString();
        if (expectedStatus == 502)
            Assert.Contains("rfc9110", type, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Equal("about:blank", type);
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("SECRET_INTERNAL_EXCEPTION_MARKER", body);
        Assert.DoesNotContain("private.example", body);
        Assert.DoesNotContain("stack trace", body, StringComparison.OrdinalIgnoreCase);
    }
}
