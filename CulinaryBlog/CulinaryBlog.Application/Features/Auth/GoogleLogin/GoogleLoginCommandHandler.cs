using System.Text.Json;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;

    public GoogleLoginCommandHandler(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IApplicationDbContext context,
        IJwtService jwtService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtService = jwtService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new UnauthorizedAccessException("Google ID token is missing.");

        using var verificationResponse = await _httpClientFactory
            .CreateClient()
            .GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken)}",
                cancellationToken);

        if (!verificationResponse.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Google rejected the ID token.");

        JsonDocument json;
        try
        {
            var payload = await verificationResponse.Content.ReadAsStringAsync(cancellationToken);
            json = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            throw new UnauthorizedAccessException("Google returned an invalid token profile.", ex);
        }

        using (json)
        {
            var root = json.RootElement;
            var configuredClientId = _configuration["Google:ClientId"];

            if (string.IsNullOrWhiteSpace(configuredClientId) ||
                !TryGetString(root, "aud", out var audience) ||
                !string.Equals(audience, configuredClientId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Google ID token audience does not match the API client.");
            }

            if (!TryGetString(root, "email", out var email))
                throw new InvalidOperationException("Google profile does not contain an email address.");

            if (!IsEmailVerified(root))
                throw new UnauthorizedAccessException("Google has not verified this account's email address.");

            if (!TryGetString(root, "sub", out var providerKey))
                throw new InvalidOperationException("Google profile does not contain a provider key.");

            var user = await _userManager.FindByLoginAsync("Google", providerKey);
            user ??= await _userManager.FindByEmailAsync(email);

            if (user is null)
                user = await CreateAuthorAsync(root, email, cancellationToken);

            if (await _userManager.IsLockedOutAsync(user))
                throw new UnauthorizedAccessException("This account is locked.");

            var loginInfo = new UserLoginInfo("Google", providerKey, "Google");
            if (await _userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey) is null)
            {
                var linkResult = await _userManager.AddLoginAsync(user, loginInfo);
                if (!linkResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ", linkResult.Errors.Select(error => error.Description)));
                }
            }

            var now = DateTime.UtcNow;
            var refreshToken = _jwtService.GenerateRefreshToken();
            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = now.AddDays(7)
            });
            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResponseDto(
                user.Id,
                user.UserName ?? string.Empty,
                user.FullName,
                user.Email ?? string.Empty,
                _jwtService.GenerateAccessToken(user),
                refreshToken,
                now.AddMinutes(15),
                now.AddDays(7));
        }
    }

    private async Task<User> CreateAuthorAsync(
        JsonElement googleProfile,
        string email,
        CancellationToken cancellationToken)
    {
        var usernameBase = new string(email.Split('@')[0]
            .Trim()
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .ToArray());
        if (string.IsNullOrWhiteSpace(usernameBase))
            usernameBase = "author";

        var username = usernameBase;
        for (var suffix = 1; await _userManager.FindByNameAsync(username) is not null; suffix++)
            username = $"{usernameBase}{suffix}";

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            FullName = TryGetString(googleProfile, "name", out var name) ? name : email,
            AvatarUrl = TryGetString(googleProfile, "picture", out var picture) ? picture : null,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, $"{Guid.NewGuid():N}Aa!1");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        const string roleName = "Author";
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!roleResult.Succeeded && !await _roleManager.RoleExistsAsync(roleName))
            {
                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException(
                    string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException(
                string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
        }

        _logger.LogInformation("Created Author account {UserId} from Google sign-in.", user.Id);
        return user;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsEmailVerified(JsonElement root)
    {
        if (!root.TryGetProperty("email_verified", out var property))
            return false;

        return property.ValueKind == JsonValueKind.True ||
            (property.ValueKind == JsonValueKind.String &&
             string.Equals(property.GetString(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
