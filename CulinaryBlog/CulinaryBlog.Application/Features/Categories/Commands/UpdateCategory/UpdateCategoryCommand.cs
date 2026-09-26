// File: CulinaryBlog.Application/Features/Categories/Commands/UpdateCategory/UpdateCategoryCommand.cs
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description
) : IRequest;