namespace CulinaryBlog.Domain.Entities;

public record RecipeNutrition(
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    decimal? Fiber = null,
    decimal? Sodium = null
);