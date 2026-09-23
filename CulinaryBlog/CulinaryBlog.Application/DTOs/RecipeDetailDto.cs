namespace CulinaryBlog.Application.DTOs;

public class RecipeDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public List<IngredientDto> Ingredients { get; set; } = new();
    public List<RecipeStepDto> Steps { get; set; } = new();
}