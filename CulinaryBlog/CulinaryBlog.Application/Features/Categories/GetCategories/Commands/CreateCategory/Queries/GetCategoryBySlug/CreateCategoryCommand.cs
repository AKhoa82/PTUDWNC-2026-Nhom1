using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name,
    string Slug,
    string? Description
) : IRequest<Guid>;