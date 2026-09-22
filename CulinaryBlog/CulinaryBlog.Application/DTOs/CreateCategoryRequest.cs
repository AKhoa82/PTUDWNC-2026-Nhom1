using System.ComponentModel.DataAnnotations;

namespace CulinaryBlog.Application.DTOs;

public record CreateCategoryRequest(
    [Required(ErrorMessage = "Tên danh mục không được để trống.")]
    [StringLength(100, ErrorMessage = "Tên danh mục không vượt quá 100 ký tự.")]
    string Name,

    string? Description,

    string? Slug
);