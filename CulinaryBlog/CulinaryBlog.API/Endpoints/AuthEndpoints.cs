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
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", Register).WithSummary("FR-AUTH-001: Đăng ký tài khoản");
        group.MapPost("/login", Login).WithSummary("FR-AUTH-002: Đăng nhập");

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

            return Results.Ok(response);
        }
        catch (AccountLockedException)
        {
            return Results.StatusCode(StatusCodes.Status423Locked);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await mediator.Send(
                new RegisterCommand(request),
                cancellationToken);

            return Results.Created(string.Empty, response);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (CulinaryBlog.Application.Common.Exceptions.ConflictException ex)
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
