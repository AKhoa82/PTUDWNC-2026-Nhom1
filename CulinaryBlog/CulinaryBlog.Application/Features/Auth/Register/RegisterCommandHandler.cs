using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLower();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email,
                cancellationToken);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "Email đã được sử dụng.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.Request.FullName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Request.Password);

        _context.Users.Add(user);

        await _context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}