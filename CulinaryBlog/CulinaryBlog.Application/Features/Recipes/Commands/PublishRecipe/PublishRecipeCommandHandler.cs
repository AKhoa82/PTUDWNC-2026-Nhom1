using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;

public class PublishRecipeCommandHandler : IRequestHandler<PublishRecipeCommand, RecipeDto>
{
    private readonly IApplicationDbContext _context;

    public PublishRecipeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDto> Handle(PublishRecipeCommand request, CancellationToken ct)
    {
        await using var transaction = await RecipeMutationTransaction.BeginAsync(_context, request.Id, ct);

        // 1. Lấy recipe kèm đầy đủ steps và ingredients để kiểm tra điều kiện publish
        var recipe = await _context.Recipes
            .Include(r => r.Steps)
            .Include(r => r.Ingredients)
            .Include(r => r.Category)
                .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");

        // 2. Kiểm tra quyền Resource-Based Authorization (chỉ Owner hoặc Admin mới được publish)
        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền xuất bản công thức này.");
        }

        // 3. Thực thi nghiệp vụ domain
        recipe.Publish();

        // 4. Lưu thay đổi
        _context.Recipes.Update(recipe);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        // 5. Trả về DTO
        var result = recipe.Adapt<RecipeDto>();
        result.RowVersion = recipe.RowVersion.ToString();
        return result;
    }
}
