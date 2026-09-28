using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public CreateCategoryCommandHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var isExist = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            
        if (isExist)
        {
            throw new ConflictException("Tên danh mục đã tồn tại!"); 
        }

        var slug = await GenerateUniqueSlugAsync(request.Name, cancellationToken);

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

        await GetCategoriesQueryHandler.InvalidateCacheAsync(_cache, cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            RecipeCount = 0
        };
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        string str = name.Trim().ToLower();
        string[] vietnameseSigns = new string[] {
            "aàảãáạăằẳẵắặâầẩẫấậ", "dđ", "eèẻẽéẹêềểễếệ", "iìỉĩíị",
            "oòỏõóọôồổỗốộơờởỡớợ", "uùủũúụưừửữứự", "yỳỷỹýỵ"
        };
        char[] replaceChars = new char[] { 'a', 'd', 'e', 'i', 'o', 'u', 'y' };
        
        for (int i = 0; i < vietnameseSigns.Length; i++)
        {
            for (int j = 0; j < vietnameseSigns[i].Length; j++)
            {
                str = str.Replace(vietnameseSigns[i][j], replaceChars[i]);
            }
        }
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        var baseSlug = Regex.Replace(str, @"\s+", "-").Trim('-');

        var slug = baseSlug;
        int counter = 1;
        
        while (await _context.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }
        return slug;
    }
}