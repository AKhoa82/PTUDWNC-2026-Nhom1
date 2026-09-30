using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

namespace CulinaryBlog.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")] 
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    // [Authorize(Roles = "Admin")] // Đã tắt bảo mật tạm thời để test
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryCommand command)
    {
        var categoryDto = await _mediator.Send(command);
        
        return CreatedAtAction(nameof(CreateCategory), new { id = categoryDto.Id }, categoryDto);
    }

    [HttpPut("{id}")] 
    // [Authorize(Roles = "Admin")] // Đã tắt bảo mật tạm thời để test
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var command = new UpdateCategoryCommand(id, request.Name, request.Description);
        
        var categoryDto = await _mediator.Send(command);

        return Ok(categoryDto);
    }
}

public record UpdateCategoryRequest(string Name, string? Description);