using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(CreateCategoryRequest Request) : IRequest<Guid>;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // 1. Tự động sinh Slug nếu client không truyền
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : request.Slug.ToLower().Trim();

        // 2. Kiểm tra trùng Slug
        var isSlugExist = await _context.Categories
            .AnyAsync(c => c.Slug == slug, cancellationToken);

        if (isSlugExist)
        {
            throw new InvalidOperationException($"Danh mục với slug '{slug}' đã tồn tại.");
        }

        // 3. Tạo Entity và lưu DB
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }

    // Hàm hỗ trợ tạo slug tiếng Việt
    private static string GenerateSlug(string text)
    {
        text = text.ToLower().Trim();
        text = Regex.Replace(text, @"[áàảãạâấầẩẫậăắằẳẵặ]", "a");
        text = Regex.Replace(text, @"[éèẻẽẹêếềểễệ]", "e");
        text = Regex.Replace(text, @"[iíìỉĩị]", "i");
        text = Regex.Replace(text, @"[óòỏõọôốồổỗộơớờởỡợ]", "o");
        text = Regex.Replace(text, @"[úùủũụưứừửữự]", "u");
        text = Regex.Replace(text, @"[ýỳỷỹỵ]", "y");
        text = Regex.Replace(text, @"[đ]", "d");
        text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\s", "-");
        return text;
    }
}