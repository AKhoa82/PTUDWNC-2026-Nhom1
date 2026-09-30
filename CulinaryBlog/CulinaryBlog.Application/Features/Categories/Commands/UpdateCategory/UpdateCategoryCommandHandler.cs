using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public UpdateCategoryCommandHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.Recipes) 
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với Id: {request.Id}");
        }

        var isDuplicateName = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != request.Id, cancellationToken);

        if (isDuplicateName)
        {
            throw new ConflictException("Tên danh mục này đã trùng với một danh mục khác.");
        }

        category.Update(request.Name.Trim(), category.Slug, request.Description?.Trim());

        await _context.SaveChangesAsync(cancellationToken);

        // Bọc khối gọi Redis vào try-catch để bỏ qua lỗi timeout khi test local
        try
        {
            await GetCategoriesQueryHandler.InvalidateCacheAsync(_cache, cancellationToken);
        }
        catch (Exception)
        {
            // Tạm thời bỏ qua lỗi Redis
        }

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            RecipeCount = category.Recipes?.Count ?? 0
        };
    }
}