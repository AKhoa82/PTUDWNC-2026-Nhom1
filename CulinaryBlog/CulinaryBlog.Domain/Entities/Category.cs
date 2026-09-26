namespace CulinaryBlog.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

    // Đổi từ protected sang public
    public Category() { }

    public Category(string name, string slug, string? description)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string slug, string? description)
    {
        Name = name;
        Slug = slug;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
}