using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, RecipeDto>
{
    private readonly IApplicationDbContext _context;

    public CreateRecipeCommandHandler(IApplicationDbContext context) 
    {
        _context = context;
    }

    public async Task<RecipeDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken) 
    {
        if (!Guid.TryParse(request.AuthorId, out var actorId) || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid authenticated author is required.");
            
        var author = await _context.Users.FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
        if (author == null)
            throw new UnauthorizedAccessException("A valid authenticated author is required.");
        
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Request.CategoryId, cancellationToken);

        if (category == null)
        {
            throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("CategoryId", "Danh mục không tồn tại.") });
        }

        var slug = await GenerateUniqueSlugAsync(request.Request.Title, cancellationToken);

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
            Status = RecipeStatus.Draft,
            CategoryId = request.Request.CategoryId,
            Category = category,
            AuthorId = actorId,

            Ingredients = request.Request.Ingredients.Select(i => RecipeIngredient.Create(
                recipeId: default,
                name: i.Name,
                quantity: i.Quantity,
                unit: i.Unit,
                notes: i.Notes,
                sortOrder: i.SortOrder
            )).ToList(),

            Steps = request.Request.Steps.Select((s, index) => RecipeStep.Create(
                recipeId: default,
                stepNumber: index + 1,
                description: s.Description,
                title: s.Title,
                durationMinutes: s.DurationMinutes,
                imageUrl: s.ImageUrl
            )).ToList()
        };

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

        _context.Recipes.Add(recipe);
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation { RecipeSlug = recipe.Slug });
        await _context.SaveChangesAsync(cancellationToken);

        var result = recipe.Adapt<RecipeDto>();
        result.RowVersion = recipe.RowVersion.ToString();
        return result;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken cancellationToken) 
    {
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

