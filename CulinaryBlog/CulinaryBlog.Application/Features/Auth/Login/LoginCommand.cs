using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Login;

public record LoginCommand(LoginRequest Request) : IRequest<AuthResponseDto>;
