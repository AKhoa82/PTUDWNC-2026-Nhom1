using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;

namespace CulinaryBlog.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")] // Thêm /v1 để đồng bộ với API lấy danh mục
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
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryCommand command)
    {
        var categoryDto = await _mediator.Send(command);
        
        // Trả về HTTP 201 Created cùng với đối tượng CategoryDto
        return CreatedAtAction(nameof(CreateCategory), new { id = categoryDto.Id }, categoryDto);
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật danh mục
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var command = new UpdateCategoryCommand(id, request.Name, request.Description);
        
        var categoryDto = await _mediator.Send(command);

        // Trả về HTTP 200 OK với đối tượng CategoryDto
        return Ok(categoryDto);
    }

    /// <summary>
    /// FR-CAT-005: Xóa danh mục
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var command = new DeleteCategoryCommand(id);
        
        await _mediator.Send(command);

        // Trả về HTTP 204 No Content
        return NoContent();
    }
}

public record UpdateCategoryRequest(string Name, string? Description);