namespace CulinaryBlog.Application.DTOs;

public class CategoryDetailDto
{
    public CategoryDto Category { get; set; } = new();
    public CulinaryBlog.Application.Common.Models.PagedResult<RecipeSummaryDto> Recipes { get; set; } = new();
}
