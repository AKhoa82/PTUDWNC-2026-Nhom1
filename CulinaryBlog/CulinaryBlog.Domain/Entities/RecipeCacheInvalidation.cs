using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public sealed class RecipeCacheInvalidation : AuditableEntity
{
    public string? RecipeSlug { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
