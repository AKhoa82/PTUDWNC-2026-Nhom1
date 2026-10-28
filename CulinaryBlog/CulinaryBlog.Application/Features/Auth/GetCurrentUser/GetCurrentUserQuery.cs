using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserProfileDto?>;