using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string Token) : IRequest<AuthResponseDto>;
