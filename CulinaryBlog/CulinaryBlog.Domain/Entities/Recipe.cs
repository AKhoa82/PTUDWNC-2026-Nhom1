namespace CulinaryBlog.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int CookingTimeMinutes { get; set; }
    public string Difficulty { get; set; } = "Easy"; // Easy, Medium, Hard

    // Quan hệ Nối về Category (Khóa ngoại)
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}