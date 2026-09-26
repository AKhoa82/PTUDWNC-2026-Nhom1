// File: CulinaryBlog.Api/Controllers/CategoriesController.cs
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

namespace CulinaryBlog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// FR-CAT-003: Tạo danh mục mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")] // Chặn quyền, chỉ Admin được phép tạo
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryCommand command)
    {
        var categoryId = await _mediator.Send(command);
        
        // Trả về HTTP 201 Created cùng với Id của danh mục mới tạo
        return CreatedAtAction(nameof(CreateCategory), new { id = categoryId }, new { Id = categoryId, Message = "Tạo danh mục thành công!" });
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật danh mục
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")] // Chặn quyền, chỉ Admin được phép cập nhật
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        // Map từ Request HTTP sang Command
        var command = new UpdateCategoryCommand(id, request.Name, request.Description);
        
        await _mediator.Send(command);

        return Ok(new { Message = "Cập nhật danh mục thành công!" });
    }
}

// Lớp phụ trợ để nhận dữ liệu từ Body (Tránh yêu cầu client phải truyền ID trong Body)
public record UpdateCategoryRequest(string Name, string? Description);