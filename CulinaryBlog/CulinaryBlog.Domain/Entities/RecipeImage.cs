namespace CulinaryBlog.Domain.Entities;

public sealed class RecipeImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid? StoredFileId { get; set; }
    public StoredFile? StoredFile { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string? MediumUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? AltText { get; set; }
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
}
