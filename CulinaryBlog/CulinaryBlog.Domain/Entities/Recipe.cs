using System.ComponentModel.DataAnnotations;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Domain.Entities;

public enum RecipeStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum RecipeDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Expert = 4
}

public class Recipe : ConcurrentEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; } = 1;
    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;
    
    public string Instructions { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string? AuthorId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public RecipeNutrition? Nutrition { get; set; }



    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();

    public void Update(
        string title, 
        string? description, 
        Guid categoryId, 
        int prepTimeMinutes, 
        int cookingTimeMinutes, 
        int servings, 
        RecipeDifficulty difficulty, 
        string instructions)
    {
        Title = title.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        PrepTimeMinutes = prepTimeMinutes;
        CookingTimeMinutes = cookingTimeMinutes;
        Servings = servings;
        Difficulty = difficulty;
        Instructions = instructions;
        // Slug không cập nhật để giữ nguyên URL (tốt cho SEO)
    }

    public void SetNutrition(decimal calories, decimal protein, decimal carbs, decimal fat, decimal? fiber = null, decimal? sodium = null)
    {
        Nutrition = new RecipeNutrition(calories, protein, carbs, fat, fiber, sodium);
    }

    public void ClearNutrition()
    {
        Nutrition = null;
    }

    public void Publish()
    {
        if (Steps is null || Steps.Count == 0)
        {
            throw new DomainException("Recipe phải có ít nhất 1 bước thực hiện trước khi xuất bản.");
        }
        if (Ingredients is null || Ingredients.Count == 0)
        {
            throw new DomainException("Recipe phải có ít nhất 1 nguyên liệu trước khi xuất bản.");
        }

        Status = RecipeStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        Status = RecipeStatus.Draft;
    }

    public void Archive() 
    {
        if (Status == RecipeStatus.Archived)
            return;

        Status = RecipeStatus.Archived;
    }

    public void Unarchive() 
    {
        if (Status != RecipeStatus.Archived)
            return;

        Status = RecipeStatus.Draft;
    }

    public void SoftDelete() 
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
    }
}
