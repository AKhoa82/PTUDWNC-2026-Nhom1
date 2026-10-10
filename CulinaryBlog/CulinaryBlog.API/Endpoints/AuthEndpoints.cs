using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Register;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.RateLimiting;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", Register).WithSummary("FR-AUTH-001: Đăng ký tài khoản");
        group.MapPost("/login", Login).WithSummary("FR-AUTH-002: Đăng nhập");
        group.MapPost("/google", GoogleLogin)
            .RequireRateLimiting(AuthRateLimitPolicy.Name)
            .WithSummary("FR-AUTH-003: Đăng nhập bằng Google")
            .AllowAnonymous()
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(
            new LoginCommand(request),
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GoogleLogin(
        GoogleLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await mediator.Send(
                new GoogleLoginCommand(request.IdToken),
                cancellationToken);

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(
                type: "about:blank",
                title: "Unauthorized",
                detail: ex.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (GoogleProfileException ex)
        {
            return Results.Problem(
                type: "about:blank",
                title: "Bad Request",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                title: "Google verification unavailable",
                detail: "Không thể liên hệ dịch vụ xác minh của Google. Vui lòng thử lại.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
    private static async Task<IResult> Register(
        RegisterRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(
            new RegisterCommand(request),
            cancellationToken);

        return Results.Created(string.Empty, response);
    }
}
