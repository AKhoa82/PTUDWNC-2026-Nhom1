using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Register;

public record RegisterCommand(RegisterRequest Request) : IRequest<Guid>;