using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public DeleteCategoryCommandHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.Recipes)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với Id: {request.Id}");
        }

        // Ràng buộc: Không cho xóa nếu danh mục đang có công thức
        if (category.Recipes.Any())
        {
            throw new ConflictException("Không thể xóa danh mục đang chứa công thức. Vui lòng chuyển công thức sang danh mục khác trước.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        // Xóa bộ nhớ đệm (Redis Cache) để danh sách cập nhật ngay lập tức
        await GetCategoriesQueryHandler.InvalidateCacheAsync(_cache, cancellationToken);
    }
}