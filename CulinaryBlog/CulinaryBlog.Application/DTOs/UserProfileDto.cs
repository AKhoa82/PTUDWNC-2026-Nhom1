namespace CulinaryBlog.Application.DTOs;

public sealed record UserProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string UserName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTime CreatedAt
);