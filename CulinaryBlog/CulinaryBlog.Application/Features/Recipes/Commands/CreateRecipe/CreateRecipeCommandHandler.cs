using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
            CreatedAt = DateTime.UtcNow
        };

        // 4. Lưu vào DB
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        return recipe.Id;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken cancellationToken) 
    {
        var baseSlug = title.ToLowerInvariant().Trim();
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