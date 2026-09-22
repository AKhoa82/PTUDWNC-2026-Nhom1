using System.Text.RegularExpressions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(CreateCategoryRequest Request) : IRequest<(Guid Id, string Slug)>;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, (Guid Id, string Slug)>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public CreateCategoryCommandHandler(
        IApplicationDbContext context, 
        IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<(Guid Id, string Slug)> Handle(
        CreateCategoryCommand command, 
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        // 1. Sinh Slug gốc từ Tên
        string baseSlug = GenerateSlug(request.Name);
        string uniqueSlug = baseSlug;
        int counter = 1;

        // 2. Tự động kiểm tra và thêm hậu tố -2, -3 nếu trùng Slug
        while (await _context.Categories.AnyAsync(c => c.Slug == uniqueSlug, cancellationToken))
        {
            counter++;
            uniqueSlug = $"{baseSlug}-{counter}";
        }

        // 3. Khởi tạo Entity và lưu Database
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = uniqueSlug,
            Description = request.Description?.Trim()
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        // 4. Xóa Cache danh mục (Cache Invalidation)
        await _cache.RemoveAsync("categories:all", cancellationToken);

        return (category.Id, category.Slug);
    }

    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant().Trim();

        // Chuyển đổi tiếng Việt có dấu thành không dấu
        str = Regex.Replace(str, @"[áàảãạăắằẳẵặâấầẩẫậ]", "a");
        str = Regex.Replace(str, @"[đ]", "d");
        str = Regex.Replace(str, @"[éèẻẽẹêếềểễệ]", "e");
        str = Regex.Replace(str, @"[íìỉĩị]", "i");
        str = Regex.Replace(str, @"[óòỏõọôốồổỗộơớờởỡợ]", "o");
        str = Regex.Replace(str, @"[úùủũụưứừửữự]", "u");
        str = Regex.Replace(str, @"[ýỳỷỹỵ]", "y");

        // Xóa ký tự đặc biệt và thay khoảng trắng bằng dấu -
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", " ").Trim();
        str = Regex.Replace(str, @"\s", "-");

        return str;
    }
}