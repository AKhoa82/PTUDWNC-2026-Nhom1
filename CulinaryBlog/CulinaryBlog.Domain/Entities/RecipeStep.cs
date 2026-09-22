namespace CulinaryBlog.Domain.Entities;

public class RecipeStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }

    public static RecipeStep Create(Guid recipeId, int stepNumber, string description, int? durationMinutes = null, string? imageUrl = null)
    {
        return new RecipeStep
        {
            RecipeId = recipeId,
            StepNumber = stepNumber,
            Description = description,
            DurationMinutes = durationMinutes,
            ImageUrl = imageUrl
        };
    }

    public void Update(string description, int? durationMinutes = null, string? imageUrl = null)
    {
        Description = description;
        DurationMinutes = durationMinutes;
        ImageUrl = imageUrl;
    }
}
