using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Contracts.Security;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}