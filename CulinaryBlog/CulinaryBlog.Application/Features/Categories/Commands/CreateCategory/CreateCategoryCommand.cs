using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name, 
    string? Description
) : IRequest<CategoryDto>;