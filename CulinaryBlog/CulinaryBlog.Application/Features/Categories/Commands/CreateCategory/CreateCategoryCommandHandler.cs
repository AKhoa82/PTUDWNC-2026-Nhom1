// File: CulinaryBlog.Application/Categories/Commands/CreateCategory/CreateCategoryCommandHandler.cs
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
// Giả định bạn có interface IApplicationDbContext

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tên danh mục đã tồn tại chưa (tuỳ chọn nhưng nên có)
        var isExist = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            
        if (isExist)
        {
            throw new Exception("Tên danh mục đã tồn tại!"); // Có thể dùng Custom Exception (ví dụ: DuplicateException)
        }

        // 2. Tạo Slug từ Name (Bạn có thể viết một Helper riêng cho việc này)
        var slug = GenerateSlug(request.Name);

        // 3. Khởi tạo Entity (Ở đây có thể dùng Mapster nếu muốn, nhưng với logic ít thuộc tính thì gọi constructor sẽ rõ ràng hơn)
        var category = new Category(request.Name, slug, request.Description);

        // 4. Lưu vào Database
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }

    // Hàm hỗ trợ tạo Slug đơn giản
    private static string GenerateSlug(string phrase)
    {
        // Bạn nên dùng một thư viện chuyên dụng như Slugify.Core hoặc viết hàm Regex xử lý tiếng Việt
        return phrase.ToLower().Replace(" ", "-"); 
    }
}