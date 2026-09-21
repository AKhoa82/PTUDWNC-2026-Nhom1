namespace CulinaryBlog.Application.DTOs;

public class RecipeSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public string Difficulty { get; set; } = "Easy";
    public string Status { get; set; } = "Draft";
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}