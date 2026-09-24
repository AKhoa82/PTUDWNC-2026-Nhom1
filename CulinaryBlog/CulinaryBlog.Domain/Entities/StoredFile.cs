namespace CulinaryBlog.Domain.Entities;

public enum StoredFileStatus { PendingUpload = 0, Active = 1, DeletePending = 2, Deleted = 3 }

public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? BucketName { get; set; }
    public string? ObjectKey { get; set; }
    public StoredFileStatus Status { get; set; } = StoredFileStatus.Active;
    public DateTime? UploadExpiresAt { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletionRequestedAt { get; set; }
    public string? DeletionJobId { get; set; }
    // Retain the identity/tombstone for audit and idempotent background deletion.
    public DateTime? DeletedAt { get; set; }
}
