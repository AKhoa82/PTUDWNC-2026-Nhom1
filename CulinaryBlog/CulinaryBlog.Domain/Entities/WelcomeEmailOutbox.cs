namespace CulinaryBlog.Domain.Entities;

public enum WelcomeEmailStatus { Pending, Queued, Sent, Failed }

public sealed class WelcomeEmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? HangfireJobId { get; set; }
    public WelcomeEmailStatus Status { get; set; }
}
