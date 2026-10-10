using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Security;

public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    private readonly IConfiguration _configuration = configuration;

    public string GenerateAccessToken(User user, IEnumerable<string>? roles = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(15);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange((roles ?? []).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role)));
        var credentials = new SigningCredentials(GetKey(), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: claims, expires: expires, signingCredentials: credentials));
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private SymmetricSecurityKey GetKey() => new(Encoding.UTF8.GetBytes(
        _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.")));
}
