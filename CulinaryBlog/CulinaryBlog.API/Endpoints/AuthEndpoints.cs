using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Register;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshTokenCookieName = "culinaryblog_refresh";

    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", Register).WithSummary("FR-AUTH-001: Đăng ký tài khoản");
        group.MapPost("/login", Login).WithSummary("FR-AUTH-002: Đăng nhập");
        group.MapPost("/google", GoogleLogin)
            .WithSummary("FR-AUTH-003: Đăng nhập bằng Google")
            .AllowAnonymous()
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);
        group.MapPost("/refresh", Refresh)
            .WithSummary("FR-AUTH-004: Làm mới access token")
            .AllowAnonymous()
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await mediator.Send(
                new GoogleLoginCommand(request.IdToken),
                cancellationToken);

            return Results.Ok(PrepareAuthResponse(response, httpContext, environment));
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

    private static async Task<IResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        [FromServices] UserManager<User> userManager,
        [FromServices] IApplicationDbContext context,
        [FromServices] IJwtService jwtService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var rawToken = environment.IsProduction()
            ? httpContext.Request.Cookies[RefreshTokenCookieName]
            : request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken)) return Results.Unauthorized();

        var hash = HashRefreshToken(rawToken);
        var storedToken = await context.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (storedToken is null) return Results.Unauthorized();

        var logger = loggerFactory.CreateLogger("CulinaryBlog.API.AuthEndpoints");
        if (storedToken.RevokedAt is not null)
        {
            logger.LogWarning("Refresh token reuse detected for user {UserId}", storedToken.UserId);
            return Results.Unauthorized();
        }

        var now = DateTime.UtcNow;
        if (storedToken.ExpiresAt <= now || storedToken.User is null ||
            await userManager.IsLockedOutAsync(storedToken.User))
            return Results.Unauthorized();

        var replacement = jwtService.GenerateRefreshToken();
        var replacementHash = HashRefreshToken(replacement);
        storedToken.RevokedAt = now;
        storedToken.ReplacedByTokenHash = replacementHash;
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = replacementHash,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrent refresh token reuse detected for user {UserId}", storedToken.UserId);
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(storedToken.User);
        var response = new AuthResponseDto(
            storedToken.User.Id,
            storedToken.User.UserName ?? string.Empty,
            storedToken.User.FullName,
            storedToken.User.Email ?? string.Empty,
            jwtService.GenerateAccessToken(storedToken.User, roles),
            replacement,
            now.AddMinutes(15),
            now.AddDays(7));
        return Results.Ok(PrepareAuthResponse(response, httpContext, environment));
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

    private static AuthResponseDto PrepareAuthResponse(
        AuthResponseDto response,
        HttpContext httpContext,
        IWebHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return response;

        httpContext.Response.Cookies.Append(
            RefreshTokenCookieName,
            response.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/api/v1/auth",
                Expires = response.RefreshTokenExpiresAt,
                MaxAge = response.RefreshTokenExpiresAt - DateTime.UtcNow,
                IsEssential = true
            });

        return response with { RefreshToken = string.Empty };
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
