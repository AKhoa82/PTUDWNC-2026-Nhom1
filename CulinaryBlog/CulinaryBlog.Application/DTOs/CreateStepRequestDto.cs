namespace CulinaryBlog.Application.DTOs;

public class CreateStepRequestDto
{
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }
}