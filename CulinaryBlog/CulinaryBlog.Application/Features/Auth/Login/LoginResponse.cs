namespace CulinaryBlog.Application.Features.Auth.Login;

public record LoginResponse(
    Guid UserId,
    string Username,
    string FullName,
    string Email);
