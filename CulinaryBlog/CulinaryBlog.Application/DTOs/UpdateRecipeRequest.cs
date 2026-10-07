using System.ComponentModel.DataAnnotations;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.DTOs;

public class UpdateRecipeRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public RecipeDifficulty Difficulty { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public RecipeNutritionDto? Nutrition { get; set; }
    
    [Required(ErrorMessage = "Dữ liệu đồng bộ (RowVersion) bị thiếu.")]
    public string RowVersion { get; set; } = string.Empty; 
}