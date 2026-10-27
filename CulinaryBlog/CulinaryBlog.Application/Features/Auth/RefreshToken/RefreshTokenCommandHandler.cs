using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.Refresh;

public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    UserManager<User> userManager,
    IJwtService jwtService,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
            throw new RefreshTokenRejectedException("AUTH_TOKEN_INVALID");

        var hash = Hash(command.Token);
        var storedToken = await context.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (storedToken is null)
            throw new RefreshTokenRejectedException("AUTH_TOKEN_INVALID");

        if (storedToken.RevokedAt is not null)
        {
            await RevokeAllSessionsAsync(storedToken.UserId, cancellationToken);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; all sessions revoked", storedToken.UserId);
            throw new RefreshTokenRejectedException("AUTH_REFRESH_TOKEN_REVOKED");
        }

        var now = DateTime.UtcNow;
        if (storedToken.ExpiresAt <= now)
            throw new RefreshTokenRejectedException("AUTH_REFRESH_TOKEN_EXPIRED");
        if (storedToken.User is null || await userManager.IsLockedOutAsync(storedToken.User))
            throw new RefreshTokenRejectedException("AUTH_TOKEN_INVALID");

        var replacement = jwtService.GenerateRefreshToken();
        var replacementHash = Hash(replacement);
        storedToken.RevokedAt = now;
        storedToken.ReplacedByTokenHash = replacementHash;
        context.RefreshTokens.Add(new CulinaryBlog.Domain.Entities.RefreshToken
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
            await RevokeAllSessionsAsync(storedToken.UserId, cancellationToken);
            logger.LogWarning("Concurrent refresh token reuse detected for user {UserId}; all sessions revoked", storedToken.UserId);
            throw new RefreshTokenRejectedException("AUTH_REFRESH_TOKEN_REVOKED");
        }

        var roles = await userManager.GetRolesAsync(storedToken.User);
        return new AuthResponseDto(
            storedToken.User.Id,
            storedToken.User.UserName ?? string.Empty,
            storedToken.User.FullName,
            storedToken.User.Email ?? string.Empty,
            jwtService.GenerateAccessToken(storedToken.User, roles),
            replacement,
            now.AddMinutes(15),
            now.AddDays(7));
    }

    private Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, DateTime.UtcNow), cancellationToken);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
