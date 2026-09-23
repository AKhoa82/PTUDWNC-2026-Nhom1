using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateRecipeCommandHandler(IApplicationDbContext context) 
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateRecipeCommand request, CancellationToken cancellationToken) 
    {
        // 1. Kiểm tra Category có tồn tại không
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.Request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Danh mục không tồn tại.");
        }

        // 2. Tạo Slug độc nhất
        var slug = await GenerateUniqueSlugAsync(request.Request.Title, cancellationToken);

        // 3. Map dữ liệu sang Entity Recipe
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = request.Request.Title.Trim(),
            Slug = slug,
            Description = request.Request.Description,
            ImageUrl = request.Request.ImageUrl,
            PrepTimeMinutes = request.Request.PrepTimeMinutes,
            CookingTimeMinutes = request.Request.CookingTimeMinutes,
            Servings = request.Request.Servings,
            Difficulty = request.Request.Difficulty,
            Instructions = request.Request.Instructions,
            Status = RecipeStatus.Draft, // Mặc định khi mới tạo là Draft
            CategoryId = request.Request.CategoryId,
            AuthorId = request.AuthorId,
            CreatedAt = DateTime.UtcNow,

            // MAP DANH SÁCH NGUYÊN LIỆU
            Ingredients = request.Request.Ingredients.Select(i => RecipeIngredient.Create(
                recipeId: default, // Sẽ tự liên kết khi recipe được thêm vào DbContext
                name: i.Name,
                quantity: i.Quantity,
                unit: i.Unit,
                notes: i.Notes,
                sortOrder: i.SortOrder
            )).ToList(),

            // MAP DANH SÁCH CÁC BƯỚC THỰC HIỆN
            Steps = request.Request.Steps.Select((s, index) => RecipeStep.Create(
                recipeId: default,
                stepNumber: index + 1, // Tự động đánh số thứ tự các bước từ 1
                description: s.Description,
                title: s.Title,
                durationMinutes: s.DurationMinutes,
                imageUrl: s.ImageUrl
            )).ToList()
        };

        // 4. Lưu vào DB
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        return recipe.Id;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken cancellationToken) 
    {
        // 1. Chuyển tiếng Việt có dấu thành không dấu
        var normalizedString = title.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        var slugWithoutAccents = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

        // 2. Format chuẩn slug (lowercase, bỏ ký tự đặc biệt, thay khoảng trắng bằng dấu gạch ngang)
        var baseSlug = slugWithoutAccents.ToLowerInvariant().Trim();
        baseSlug = Regex.Replace(baseSlug, @"[^a-z0-9\s-]", "");
        baseSlug = Regex.Replace(baseSlug, @"\s+", "-").Trim('-');
        
        var slug = baseSlug;
        var counter = 1;
        while (await _context.Recipes.AnyAsync(r => r.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }
        return slug;
    }
}