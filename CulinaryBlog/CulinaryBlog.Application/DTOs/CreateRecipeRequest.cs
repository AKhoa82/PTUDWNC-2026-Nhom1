using System.ComponentModel.DataAnnotations;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.DTOs;

public class CreateRecipeRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; } 

    [Range(0, 1000, ErrorMessage = "Thời gian chuẩn bị không hợp lệ")]
    public int PrepTimeMinutes { get; set; }

    [Range(0, 1000, ErrorMessage = "Thời gian nấu không hợp lệ")]
    public int CookingTimeMinutes { get; set; }

    [Range(1, 100, ErrorMessage = "Số khẩu phần ăn không hợp lệ")]
    public int Servings { get; set; }

    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;

    [Required(ErrorMessage = "Hướng dẫn không được để trống")]
    public string Instructions { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }
}
