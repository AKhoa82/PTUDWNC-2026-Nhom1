using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Register;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/register", Register);
        endpoints.MapPost("/api/auth/login", Login);
        // Frontend uses: {apiUrl}/v1/auth/google
        endpoints.MapPost("/api/v1/auth/google", GoogleLogin);

        return endpoints;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await mediator.Send(
                new LoginCommand(request),
                cancellationToken);

            return Results.Ok(new
            {
                message = "Đăng nhập thành công.",
                user = response
            });
        }
        catch (AccountLockedException)
        {
            return Results.StatusCode(StatusCodes.Status423Locked);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Database unavailable",
                detail: "Không thể kết nối đến cơ sở dữ liệu. Vui lòng thử lại sau.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GoogleLogin(
        GoogleLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return Results.BadRequest(new { message = "Thiếu Google ID token." });

        try
        {
            var response = await mediator.Send(
                new GoogleLoginCommand(request.IdToken),
                cancellationToken);

            return Results.Ok(new
            {
                message = "Đăng nhập Google thành công.",
                user = response
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(
                new { message = ex.Message },
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
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
        try
        {
            var userId = await mediator.Send(
                new RegisterCommand(request),
                cancellationToken);

            return Results.Ok(new
            {
                message = "Đăng ký thành công.",
                userId
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new
            {
                message = ex.Message
            });
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Results.Conflict(new
            {
                message = "Email hoặc tên định danh đã được sử dụng."
            });
        }
    }
}
