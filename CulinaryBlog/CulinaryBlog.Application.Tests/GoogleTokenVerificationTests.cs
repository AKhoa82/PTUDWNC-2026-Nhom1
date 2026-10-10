using System.Reflection;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleTokenVerificationTests
{
    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJhdWQiOiJ0ZXN0LWNsaWVudCIsImlzcyI6Imh0dHBzOi8vYWNjb3VudHMuZ29vZ2xlLmNvbSIsImV4cCI6NDA3MDkwODgwMH0.signature")]
    public async Task Malformed_or_unsigned_id_token_is_rejected_by_production_verifier(string token)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GoogleIdTokenVerifier().VerifyAsync(token, "test-client", default));
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(GoogleProfileException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(HttpRequestException), StatusCodes.Status502BadGateway)]
    public async Task Google_endpoint_maps_only_expected_auth_and_profile_failures(
        Type exceptionType, int expectedStatus)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(exceptionType, "test error")!);

        var result = await InvokeEndpointAsync(mediator.Object);
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    [Fact]
    public async Task Google_endpoint_does_not_map_infrastructure_failure_to_bad_request()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(service => service.Send(It.IsAny<GoogleLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("configuration unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeEndpointAsync(mediator.Object));
    }

    private static async Task<IResult> InvokeEndpointAsync(IMediator mediator)
    {
        var method = typeof(AuthEndpoints).GetMethod("GoogleLogin", BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task<IResult>)method.Invoke(null, [new GoogleLoginRequest("test-token"), mediator, CancellationToken.None])!;
        return await task;
    }
}
