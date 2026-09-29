using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;

public class PublishRecipeCommandHandler : IRequestHandler<PublishRecipeCommand, RecipeDetailDto>
{
    private readonly IApplicationDbContext _context;

    public PublishRecipeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDetailDto> Handle(PublishRecipeCommand request, CancellationToken ct)
    {
        // 1. Lấy recipe kèm đầy đủ steps và ingredients để kiểm tra điều kiện publish
        var recipe = await _context.Recipes
            .Include(r => r.Steps)
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");

        // 2. Kiểm tra quyền Resource-Based Authorization (chỉ Owner hoặc Admin mới được publish)
        if (recipe.AuthorId != request.CurrentUserId && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xuất bản công thức này.");
        }

        // 3. Thực thi nghiệp vụ domain
        recipe.Publish();

        // 4. Lưu thay đổi
        _context.Recipes.Update(recipe);
        await _context.SaveChangesAsync(ct);

        // 5. Trả về DTO
        return recipe.Adapt<RecipeDetailDto>();
    }
}