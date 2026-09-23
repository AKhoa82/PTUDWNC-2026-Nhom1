namespace CulinaryBlog.Domain.Entities;

public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Keep ownership after deletion so repeat DELETE requests remain authorized.
    public DateTime? DeletedAt { get; set; }
}
