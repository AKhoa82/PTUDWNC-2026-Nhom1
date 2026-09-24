namespace CulinaryBlog.Application.DTOs;

public sealed record RecipeImageDto(
    Guid ImageId,
    string OriginalUrl,
    string? MediumUrl,
    string? ThumbnailUrl,
    string? AltText,
    bool IsPrimary,
    int OrderIndex);

public sealed class UpdateRecipeImageRequest
{
    private string? _altText;
    public string? AltText
    {
        get => _altText;
        set { _altText = value; AltTextSpecified = true; }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool AltTextSpecified { get; private set; }
    public bool? IsPrimary { get; set; }
    public int? OrderIndex { get; set; }
}
