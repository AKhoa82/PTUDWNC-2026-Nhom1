using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

public class UpdateRecipeCommandHandler : IRequestHandler<UpdateRecipeCommand, RecipeDetailDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateRecipeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeDetailDto> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Category)
            .Include(r => r.Steps)
            .Include(r => r.Ingredients)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
        {
            throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");
        }

        // Kiểm tra quyền (Resource-Based Authorization)
        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền chỉnh sửa công thức này.");
        }

        // Kiểm tra danh mục
        if (!await _context.Categories.AnyAsync(c => c.Id == request.Request.CategoryId, cancellationToken))
        {
            throw new FluentValidation.ValidationException(new[] {
                new FluentValidation.Results.ValidationFailure(
                    "CategoryId",
                    "Danh mục không tồn tại.")
            });
        }

        // Gán OriginalValue cho RowVersion để EF Core phát hiện thay đổi đồng thời
        if (!uint.TryParse(request.Request.RowVersion, out var clientRowVersion))
        {
            throw new FluentValidation.ValidationException(new[] {
                new FluentValidation.Results.ValidationFailure(
                    "RowVersion",
                    "RowVersion không hợp lệ.")
            });
        }
        _context.Recipes.Entry(recipe).Property(r => r.RowVersion).OriginalValue = clientRowVersion;

        recipe.Update(
            request.Request.Title,
            request.Request.Description,
            request.Request.CategoryId,
            request.Request.PrepTimeMinutes,
            request.Request.CookingTimeMinutes,
            request.Request.Servings,
            request.Request.Difficulty,
            request.Request.Instructions
        );

        if (request.Request.Nutrition != null)
        {
            recipe.SetNutrition(
                request.Request.Nutrition.Calories,
                request.Request.Nutrition.Protein,
                request.Request.Nutrition.Carbs,
                request.Request.Nutrition.Fat,
                request.Request.Nutrition.Fiber,
                request.Request.Nutrition.Sodium
            );
        }

        try
        {
            _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Bắn ra 409 Conflict khi có xung đột dữ liệu
            throw new ConflictException("Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.");
        }

        var result = recipe.Adapt<RecipeDetailDto>();
        result.RowVersion = recipe.RowVersion.ToString();
        return result;
    }
}