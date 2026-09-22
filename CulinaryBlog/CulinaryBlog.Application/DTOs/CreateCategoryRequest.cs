using System.ComponentModel.DataAnnotations;

namespace CulinaryBlog.Application.DTOs;

public record CreateCategoryRequest(
    [Required(ErrorMessage = "Tên danh mục không được để trống.")]
    [StringLength(100, ErrorMessage = "Tên danh mục không vượt quá 100 ký tự.")]
    string Name,

    [StringLength(500, ErrorMessage = "Mô tả không vượt quá 500 ký tự.")]
    string? Description
);