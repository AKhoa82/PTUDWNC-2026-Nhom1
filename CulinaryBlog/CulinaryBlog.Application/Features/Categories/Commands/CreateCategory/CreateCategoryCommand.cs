// File: CulinaryBlog.Application/Categories/Commands/CreateCategory/CreateCategoryCommand.cs
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

// Trả về Guid chính là Id của Category vừa tạo
public record CreateCategoryCommand(
    string Name, 
    string? Description
) : IRequest<Guid>;