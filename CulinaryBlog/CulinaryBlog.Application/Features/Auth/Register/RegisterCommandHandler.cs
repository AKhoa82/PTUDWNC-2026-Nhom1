using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Guid> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var username = request.Request.Username.Trim().ToLowerInvariant();
        var email = request.Request.Email.Trim().ToLower();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email!.ToLower() == email || u.UserName!.ToLower() == username,
                cancellationToken);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
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
            throw new InvalidOperationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }
}