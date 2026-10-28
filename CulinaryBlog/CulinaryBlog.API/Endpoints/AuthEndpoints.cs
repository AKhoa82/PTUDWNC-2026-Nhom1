using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Application.Features.Auth.Refresh;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using CulinaryBlog.Application.Features.Auth.GetCurrentUser;
using CulinaryBlog.Application.Features.Auth.UpdateProfile;
using System.Text.Json;

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
        group.MapPost("/logout", Logout)
            .AllowAnonymous()
            .WithSummary("FR-AUTH-005: Đăng xuất và thu hồi refresh token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
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

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization("AuthorPolicy")
            .WithName("GetCurrentUserProfile")
            .WithSummary("FR-AUTH-006: Xem hồ sơ cá nhân")
            .Produces<UserProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/me", UpdateCurrentUser)
            .RequireAuthorization("AuthorPolicy")
            .WithName("UpdateCurrentUserProfile")
            .WithSummary("FR-AUTH-007: Cập nhật hồ sơ cá nhân")
            .Produces<UserProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(
                StatusCodes.Status422UnprocessableEntity);
        return endpoints;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IMediator mediator,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(
            new LoginCommand(request),
            cancellationToken);

        return Results.Ok(PrepareAuthResponse(response, httpContext, environment));
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

    private static async Task<IResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        ClaimsPrincipal user,
        IOptionsMonitor<JwtBearerOptions> jwtOptions,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var rawToken = environment.IsProduction()
            ? httpContext.Request.Cookies[RefreshTokenCookieName]
            : request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken))
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
        else if (httpContext.Request.Headers.Authorization is { Count: > 0 })
        {
            // JWT hết hạn được chấp nhận riêng cho logout khi refresh token còn được gửi kèm.
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            try
            {
                var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.Clone();
                parameters.ValidateLifetime = false;
                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    authorization["Bearer ".Length..].Trim(), parameters, out var validatedToken);
                if (validatedToken.ValidTo == DateTime.MinValue || validatedToken.ValidTo > DateTime.UtcNow)
                    return Results.Unauthorized();

                var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (!Guid.TryParse(userIdClaim, out var parsedUserId))
                    return Results.Unauthorized();
                userId = parsedUserId;
            }
            catch (SecurityTokenException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException)
            {
                return Results.Unauthorized();
            }
        }
        else
        {
            return Results.Unauthorized();
        }

        await mediator.Send(
            new LogoutCommand(userId, rawToken),
            cancellationToken);

        if (environment.IsProduction())
        {
            httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                Path = "/api/v1/auth",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var rawToken = environment.IsProduction()
            ? httpContext.Request.Cookies[RefreshTokenCookieName]
            : request?.RefreshToken;
        try
        {
            var response = await mediator.Send(new RefreshTokenCommand(rawToken ?? string.Empty), cancellationToken);
            return Results.Ok(PrepareAuthResponse(response, httpContext, environment));
        }
        catch (RefreshTokenRejectedException ex)
        {
            return Results.Problem(
                type: $"urn:culinaryblog:{ex.Code}",
                title: "Unauthorized",
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }
    private static async Task<IResult> Register(
        RegisterRequest request,
        IMediator mediator,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(
            new RegisterCommand(request),
            cancellationToken);

        return Results.Created(string.Empty, PrepareAuthResponse(response, httpContext, environment));
    }

    private static async Task<IResult> GetCurrentUser(ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var profile = await mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);

        if (profile is null)
        {
            return Results.Problem(
                title: "User Not Found",
                detail: "Không tìm thấy tài khoản người dùng.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(profile);
    }

    private static async Task<IResult> UpdateCurrentUser(
    HttpRequest request,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken cancellationToken)
    {
        var userIdClaim =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        static IResult InvalidBody(string message)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["body"] = [message]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        JsonDocument bodyDocument;
        try
        {
            bodyDocument = await JsonDocument.ParseAsync(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidBody("Dữ liệu phải là JSON hợp lệ.");
        }

        using var document = bodyDocument;
        var body = document.RootElement;

        if (body.ValueKind != JsonValueKind.Object)
        {
            return InvalidBody("Dữ liệu phải là một JSON object.");
        }

        var properties = body.EnumerateObject().ToArray();

        // Không cho cập nhật Email, UserName hay field khác.
        if (properties.Any(p =>
                p.Name != "fullName"
                && p.Name != "avatarUrl"))
        {
            return InvalidBody(
                "Chỉ được cập nhật fullName và avatarUrl.");
        }

        // Tránh các key trùng lặp trong JSON.
        if (properties.GroupBy(p => p.Name)
            .Any(group => group.Count() > 1))
        {
            return InvalidBody(
                "Dữ liệu không được chứa tên thuộc tính trùng lặp.");
        }

        var hasFullName = body.TryGetProperty(
            "fullName", out var fullNameElement);

        var hasAvatarUrl = body.TryGetProperty(
            "avatarUrl", out var avatarUrlElement);

        if (hasFullName &&
            fullNameElement.ValueKind != JsonValueKind.String)
        {
            return InvalidBody(
                "fullName phải là chuỗi ký tự.");
        }

        if (hasAvatarUrl &&
            avatarUrlElement.ValueKind != JsonValueKind.String &&
            avatarUrlElement.ValueKind != JsonValueKind.Null)
        {
            return InvalidBody(
                "avatarUrl phải là chuỗi URL hoặc null.");
        }

        var fullName = hasFullName
            ? fullNameElement.GetString()
            : null;

        var avatarUrl =
            hasAvatarUrl &&
            avatarUrlElement.ValueKind == JsonValueKind.String
                ? avatarUrlElement.GetString()
                : null;

        var command = new UpdateProfileCommand(
            UserId: userId,
            HasFullName: hasFullName,
            FullName: fullName,
            HasAvatarUrl: hasAvatarUrl,
            AvatarUrl: avatarUrl
        );

        var updatedProfile = await mediator.Send(
            command,
            cancellationToken);

        return Results.Ok(updatedProfile);
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
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                Expires = response.RefreshTokenExpiresAt,
                MaxAge = response.RefreshTokenExpiresAt - DateTime.UtcNow,
                IsEssential = true
            });

        return response with { RefreshToken = string.Empty };
    }

}
