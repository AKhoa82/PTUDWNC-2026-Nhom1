using System.Text.Json;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Domain.Entities;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
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
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IApplicationDbContext context,
        IJwtService jwtService,
        HttpClient httpClient,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Results.BadRequest(new { message = "Thiếu Google ID token." });
        }

        try
        {
            var verificationUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken)}";
            using var verificationResponse = await httpClient.GetAsync(verificationUrl, cancellationToken);

            if (!verificationResponse.IsSuccessStatusCode)
            {
                return Results.Json(
                    new { message = "Google từ chối ID token. Kiểm tra Client ID của frontend và cấu hình Google Cloud." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var payload = await verificationResponse.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            var configuredClientId = configuration["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(configuredClientId) ||
                !root.TryGetProperty("aud", out var audienceElement) ||
                !string.Equals(audienceElement.GetString(), configuredClientId, StringComparison.Ordinal))
            {
                return Results.Json(
                    new { message = "Client ID trong token Google không khớp với Google:ClientId của API." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!root.TryGetProperty("email", out var emailElement) ||
                string.IsNullOrWhiteSpace(emailElement.GetString()))
            {
                return Results.BadRequest(new { message = "Google profile không hợp lệ." });
            }

            if (!root.TryGetProperty("email_verified", out var emailVerifiedElement) ||
                !(emailVerifiedElement.ValueKind == JsonValueKind.True ||
                  (emailVerifiedElement.ValueKind == JsonValueKind.String &&
                   string.Equals(emailVerifiedElement.GetString(), "true", StringComparison.OrdinalIgnoreCase))))
            {
                return Results.Json(
                    new { message = "Google chưa xác minh email của tài khoản này." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var email = emailElement.GetString()!;
            var providerKey = root.TryGetProperty("sub", out var subElement)
                ? subElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return Results.BadRequest(new { message = "Google profile thiếu provider key." });
            }

            var existingUserByLogin = await userManager.FindByLoginAsync("Google", providerKey);
            var user = existingUserByLogin ?? await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                var usernameBase = email.Split('@')[0].Trim();
                var username = usernameBase;
                var suffix = 1;

                while (await userManager.FindByNameAsync(username) is not null)
                {
                    username = $"{usernameBase}{suffix++}";
                }

                user = new User
                {
                    Id = Guid.NewGuid(),
                    UserName = username,
                    Email = email,
                    FullName = root.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? email
                        : email,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(
                    user,
                    Guid.NewGuid().ToString("N") + "Aa!1");

                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ", createResult.Errors.Select(error => error.Description)));
                }

                var roleName = "Author";
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }

                await userManager.AddToRoleAsync(user, roleName);
            }

            var loginInfo = new UserLoginInfo("Google", providerKey, "Google");
            if (await userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey) is null)
            {
                var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
                if (!addLoginResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ", addLoginResult.Errors.Select(error => error.Description)));
                }
            }

            var now = DateTime.UtcNow;
            var refreshToken = jwtService.GenerateRefreshToken();
            context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = now.AddDays(7)
            });
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                message = "Đăng nhập Google thành công.",
                user = new AuthResponseDto(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.FullName,
                    user.Email ?? string.Empty,
                    jwtService.GenerateAccessToken(user),
                    refreshToken,
                    now.AddMinutes(15),
                    now.AddDays(7))
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Google login failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
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
