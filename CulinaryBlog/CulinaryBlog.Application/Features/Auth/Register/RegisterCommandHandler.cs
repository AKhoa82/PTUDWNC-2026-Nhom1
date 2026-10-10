using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        UserManager<User> userManager,
        IJwtService jwtService)
    {
        _context = context;
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var username = request.Request.Username.Trim().ToLowerInvariant();
        var email = request.Request.Email.Trim().ToLowerInvariant();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email!.ToLower() == email || u.UserName!.ToLower() == username,
                cancellationToken);

        if (existingUser is not null)
        {
            throw new CulinaryBlog.Application.Common.Exceptions.ConflictException(
                    existingUser.Email!.ToLower() == email
                    ? "Email đã được sử dụng."
                    : "Tên định danh đã được sử dụng.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            FullName = request.Request.FullName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Request.Password);
        if (!result.Succeeded)
        {
            var conflictErrors = result.Errors.Where(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail").ToList();
            if (conflictErrors.Any())
            {
                throw new CulinaryBlog.Application.Common.Exceptions.ConflictException(
                    string.Join(" ", conflictErrors.Select(e => e.Description)));
            }

            var failures = result.Errors.Select(e =>
            {
                var propertyName = e.Code switch
                {
                    "InvalidUserName" => "Username",
                    "InvalidEmail" => "Email",
                    _ when e.Code.StartsWith("Password") => "Password",
                    _ => "User"
                };
                return new FluentValidation.Results.ValidationFailure(propertyName, e.Description);
            }).ToList();

            throw new FluentValidation.ValidationException(failures);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Author");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        // Auto-login logic
        var roles = await _userManager.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var refreshToken = _jwtService.GenerateRefreshToken();
        
        using var sha256 = SHA256.Create();
        var hashedToken = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
        
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hashedToken,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            throw;
        }

        return new AuthResponseDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName,
            user.Email ?? string.Empty,
            _jwtService.GenerateAccessToken(user, roles),
            refreshToken,
            now.AddMinutes(15),
            now.AddDays(7)
        );
    }
}
