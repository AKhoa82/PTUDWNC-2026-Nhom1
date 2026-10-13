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
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với Id: {request.Id}");
        }

        // Đếm toàn bộ công thức tham chiếu, BỎ QUA Query Filter để gom cả công thức đã xóa mềm
        var recipeCount = await _context.Recipes
            .IgnoreQueryFilters()
            .CountAsync(r => r.CategoryId == request.Id, cancellationToken);

        if (recipeCount > 0)
        {
            // Trả về ConflictException (HTTP 409) kèm theo số lượng công thức cụ thể
            throw new ConflictException($"Không thể xóa danh mục này. Đang có {recipeCount} công thức phụ thuộc (bao gồm cả công thức đang bị ẩn hoặc xóa mềm).");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        // Xóa bộ nhớ đệm (Redis Cache) để danh sách cập nhật ngay lập tức
        await GetCategoriesQueryHandler.InvalidateCacheAsync(_cache, cancellationToken);
    }
}