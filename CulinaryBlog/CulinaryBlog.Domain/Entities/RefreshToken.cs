using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    // Stores the SHA-256 hash of the raw token. A legacy plaintext value is
    // upgraded the first time that token is refreshed.
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
