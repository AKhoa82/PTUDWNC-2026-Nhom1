using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserProfileDto?>
{
    private readonly UserManager<User> _userManager;

    public GetCurrentUserQueryHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserProfileDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(
            request.UserId.ToString());

        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserProfileDto(
            Id: user.Id,
            FullName: user.FullName,
            Email: user.Email ?? string.Empty,
            UserName: user.UserName ?? string.Empty,
            AvatarUrl: user.AvatarUrl,
            Roles: roles.ToArray(),
            EmailConfirmed: user.EmailConfirmed,
            CreatedAt: user.CreatedAt
        );
    }
}