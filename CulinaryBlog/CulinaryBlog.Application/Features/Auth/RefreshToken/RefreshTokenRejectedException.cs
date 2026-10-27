namespace CulinaryBlog.Application.Features.Auth.Refresh;

public sealed class RefreshTokenRejectedException(string code) : UnauthorizedAccessException
{
    public string Code { get; } = code;
}
