namespace CulinaryBlog.Domain.Entities;

public sealed class RecipeCacheInvalidation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
