using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _context;

    public LogoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)))
            .ToLowerInvariant();

        var refreshToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(token =>
                token.TokenHash == tokenHash &&
                (request.UserId == null || token.UserId == request.UserId),
                cancellationToken);

        // Logout is idempotent: missing, foreign, or already-revoked tokens
        // produce the same successful response without revealing token state.
        if (refreshToken is null || refreshToken.RevokedAt is not null)
            return;

        refreshToken.RevokedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai yêu cầu logout đồng thời vẫn có cùng kết quả idempotent.
        }
    }
}
