using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeIngredient : AuditableEntity
{
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; } 
    public int SortOrder { get; set; }
    
    public Recipe Recipe { get; set; } = null!;

    public static RecipeIngredient Create(Guid recipeId, string name, decimal? quantity, string? unit, string? notes, int sortOrder)
    {
        return new RecipeIngredient
        {
            RecipeId = recipeId,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Notes = notes,
            SortOrder = sortOrder
        };
    }

    public void Update(string name, decimal? quantity, string? unit, string? notes, int sortOrder)
    {
        Name = name;
        Quantity = quantity;
        Unit = unit;
        Notes = notes;
        SortOrder = sortOrder;
    }
}