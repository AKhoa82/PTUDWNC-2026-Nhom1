using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileCommandHandler
    : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    private readonly UserManager<User> _userManager;

    public UpdateProfileCommandHandler(
        UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserProfileDto> Handle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(
            request.UserId.ToString());

        if (user is null)
        {
            throw new NotFoundException(
                "Không tìm thấy tài khoản người dùng.");
        }

        // Chỉ cập nhật những trường được gửi lên.
        if (request.HasFullName)
        {
            user.FullName = request.FullName!.Trim();
        }

        if (request.HasAvatarUrl)
        {
            user.AvatarUrl = request.AvatarUrl?.Trim();
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new ConflictException(
                string.Join("; ",
                    result.Errors.Select(error =>
                        error.Description)));
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