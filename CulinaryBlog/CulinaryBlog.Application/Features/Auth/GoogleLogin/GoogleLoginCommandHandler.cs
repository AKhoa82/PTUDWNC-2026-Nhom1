using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IGoogleIdTokenVerifier _tokenVerifier;
    private readonly IGoogleLoginTransactionFactory _transactionFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;

    public GoogleLoginCommandHandler(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IApplicationDbContext context,
        IJwtService jwtService,
        IGoogleIdTokenVerifier tokenVerifier,
        IGoogleLoginTransactionFactory transactionFactory,
        IConfiguration configuration,
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtService = jwtService;
        _tokenVerifier = tokenVerifier;
        _transactionFactory = transactionFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        var configuredClientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(configuredClientId))
            throw new InvalidOperationException("Google:ClientId is not configured.");

        var profile = await _tokenVerifier.VerifyAsync(request.IdToken, configuredClientId, cancellationToken);
        if (string.IsNullOrWhiteSpace(profile.Email))
            throw new GoogleProfileException("Google profile does not contain an email address.");
        if (!profile.EmailVerified)
            throw new UnauthorizedAccessException("Google has not verified this account's email address.");
        if (string.IsNullOrWhiteSpace(profile.Subject))
            throw new GoogleProfileException("Google profile does not contain a provider key.");

        var email = profile.Email;
        var providerKey = profile.Subject;
        var normalizedEmail = _userManager.NormalizeEmail(email) ?? email.ToUpperInvariant();
        await using var transaction = await _transactionFactory.BeginAsync(
            providerKey, normalizedEmail, cancellationToken);

        var user = await _userManager.FindByLoginAsync("Google", providerKey);
        user ??= await _userManager.FindByEmailAsync(email);

        var createdAuthor = user is null;
        if (user is null)
            user = await CreateAuthorAsync(profile, email, cancellationToken);

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
        var roles = await _userManager.GetRolesAsync(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)))
            .ToLowerInvariant();
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hashedToken,
            ExpiresAt = now.AddDays(7)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var response = new AuthResponseDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName,
            user.Email ?? string.Empty,
            _jwtService.GenerateAccessToken(user, roles),
            refreshToken,
            now.AddMinutes(15),
            now.AddDays(7));
        await transaction.CommitAsync(cancellationToken);
        if (createdAuthor)
            _logger.LogInformation("Created Author account {UserId} from Google sign-in.", user.Id);
        return response;
    }

    private async Task<User> CreateAuthorAsync(
        GoogleIdTokenProfile googleProfile,
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
            FullName = string.IsNullOrWhiteSpace(googleProfile.Name) ? email : googleProfile.Name,
            AvatarUrl = string.IsNullOrWhiteSpace(googleProfile.Picture) ? null : googleProfile.Picture,
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
                throw new InvalidOperationException(
                    string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
        }

        return user;
    }
}
