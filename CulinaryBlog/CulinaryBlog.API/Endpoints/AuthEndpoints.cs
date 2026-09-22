using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using FluentValidation;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/register", Register);
        endpoints.MapPost("/api/auth/login", Login);

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
