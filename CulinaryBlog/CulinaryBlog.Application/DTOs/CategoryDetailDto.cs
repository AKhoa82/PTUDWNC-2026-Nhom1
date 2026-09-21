namespace CulinaryBlog.Application.DTOs;

public class CategoryDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<RecipeSummaryDto> Recipes { get; set; } = new();
}