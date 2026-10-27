using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

public static class RecipeSlugGenerator
{
    public static string GenerateBaseSlug(string title)
    {
        var normalizedTitle = title.Trim().Normalize(NormalizationForm.FormD);
        var withoutAccents = new StringBuilder(normalizedTitle.Length);

        foreach (var character in normalizedTitle)
        {
            if (character is 'đ' or 'Đ')
            {
                withoutAccents.Append('d');
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                withoutAccents.Append(character);
            }
        }

        var slug = withoutAccents.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        return Regex.Replace(slug, @"\s+", "-").Trim('-');
    }
}
