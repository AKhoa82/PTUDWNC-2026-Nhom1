namespace CulinaryBlog.Application.DTOs;

public record AuthResponseDto(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);