using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        UserManager<User> userManager,
        IApplicationDbContext context,
        IJwtService jwtService)
    {
        _userManager = userManager;
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Request.Email.Trim());
        if (user is null)
        {
            throw new UnauthorizedAccessException();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new AccountLockedException();
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException();
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var refreshToken = _jwtService.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, Token = refreshToken,
            ExpiresAt = now.AddDays(7)
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            user.Id, user.UserName ?? string.Empty, user.FullName, user.Email ?? string.Empty,
            _jwtService.GenerateAccessToken(user, roles), refreshToken,
            now.AddMinutes(15), now.AddDays(7));
    }
}
