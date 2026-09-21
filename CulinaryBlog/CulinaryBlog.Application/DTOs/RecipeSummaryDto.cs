namespace CulinaryBlog.Application.DTOs;

public class RecipeSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int CookingTimeMinutes { get; set; }
    public string Difficulty { get; set; } = "Easy";
    public DateTime CreatedAt { get; set; }
}