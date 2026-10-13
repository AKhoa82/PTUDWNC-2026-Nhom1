using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using FluentValidation;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", Register).WithSummary("FR-AUTH-001: Đăng ký tài khoản");
        group.MapPost("/login", Login).WithSummary("FR-AUTH-002: Đăng nhập");
        group.MapPost("/logout", Logout)
            .AllowAnonymous()
            .WithSummary("FR-AUTH-005: Đăng xuất và thu hồi refresh token");

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

    private static async Task<IResult> Logout(
        LogoutRequest? request,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
            return Results.Unauthorized();

        Guid? userId = null;
        if (user.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(userIdClaim, out var parsedUserId))
                return Results.Unauthorized();

            userId = parsedUserId;
        }

        await mediator.Send(
            new LogoutCommand(userId, request.RefreshToken),
            cancellationToken);

        return Results.NoContent();
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
