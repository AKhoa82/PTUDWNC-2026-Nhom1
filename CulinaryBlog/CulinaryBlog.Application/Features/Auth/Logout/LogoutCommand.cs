using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Logout;

public sealed record LogoutCommand(Guid? UserId, string RefreshToken) : IRequest;
