using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.UpdateProfile;

public sealed record UpdateProfileCommand(
    Guid UserId,
    bool HasFullName,
    string? FullName,
    bool HasAvatarUrl,
    string? AvatarUrl
) : IRequest<UserProfileDto>;