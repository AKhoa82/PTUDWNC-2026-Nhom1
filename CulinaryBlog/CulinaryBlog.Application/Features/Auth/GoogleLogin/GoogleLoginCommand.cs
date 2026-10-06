using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed record GoogleLoginCommand(string IdToken) : IRequest<AuthResponseDto>;
