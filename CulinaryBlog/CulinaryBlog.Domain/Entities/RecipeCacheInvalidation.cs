using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public sealed class RecipeCacheInvalidation : AuditableEntity
{
    public DateTime? ProcessedAt { get; set; }
}
