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

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        // Tự động tạo slug từ tên danh mục
        var slug = GenerateSlug(req.Name);

        // Kiểm tra xem tên hoặc slug đã tồn tại chưa
        var isExisted = await _context.Categories
            .AnyAsync(c => c.Slug == slug || c.Name.ToLower() == req.Name.ToLower(), cancellationToken);

        if (isExisted)
        {
            throw new InvalidOperationException($"Danh mục '{req.Name}' đã tồn tại trong hệ thống.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Slug = slug,
            Description = req.Description
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }

    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLower().Trim();
        str = Regex.Replace(str, @"[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        str = Regex.Replace(str, @"[èéẹẻẽêềếệểễ]", "e");
        str = Regex.Replace(str, @"[ìíịỉĩ]", "i");
        str = Regex.Replace(str, @"[òóọỏõôồốộổỗơờớợởỡ]", "o");
        str = Regex.Replace(str, @"[ùúụủũưừứựửữ]", "u");
        str = Regex.Replace(str, @"[ỳýỵỷỹ]", "y");
        str = Regex.Replace(str, @"đ", "d");
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", " ").Trim();
        str = Regex.Replace(str, @"\s", "-");
        return str;
    }
}