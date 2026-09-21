using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public CreateCategoryCommandHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await _context.Categories
            .AnyAsync(c => c.Slug == request.Slug, cancellationToken);

        if (slugExists)
        {
            throw new InvalidOperationException($"Slug '{request.Slug}' đã tồn tại.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync("categories:all", cancellationToken);

        return category.Id;
    }
}