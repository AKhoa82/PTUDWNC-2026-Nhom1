using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;

internal static class SampleRecipeContent
{
    private sealed record Template(
        (string Name, decimal Quantity, string Unit)[] Ingredients,
        string[] Steps);

    private static readonly IReadOnlyDictionary<string, Template> Templates =
        new Dictionary<string, Template>(StringComparer.Ordinal)
        {
            ["pho-bo-ha-noi"] = new(
                [("Xương bò", 500m, "g"), ("Bánh phở", 400m, "g"), ("Thịt bò", 300m, "g")],
                ["Hầm xương bò với gia vị để lấy nước dùng trong.", "Trụng bánh phở, xếp thịt bò vào tô rồi chan nước dùng nóng."]),
            ["bun-bo-hue"] = new(
                [("Thịt bò", 400m, "g"), ("Bún", 400m, "g"), ("Sả", 3m, "cây")],
                ["Hầm thịt bò với sả cho mềm và nêm nước dùng vừa ăn.", "Trụng bún, xếp thịt vào tô rồi chan nước dùng nóng."]),
            ["com-chien-duong-chau"] = new(
                [("Cơm nguội", 400m, "g"), ("Tôm", 150m, "g"), ("Trứng", 2m, "quả")],
                ["Sơ chế tôm và đánh tan trứng.", "Chiên trứng và tôm, thêm cơm rồi đảo đều đến khi hạt cơm săn lại."]),
            ["mi-ramen-nhat-ban"] = new(
                [("Xương heo", 700m, "g"), ("Mì ramen", 300m, "g"), ("Thịt chashu", 200m, "g")],
                ["Hầm xương heo để lấy nước dùng đậm vị.", "Luộc mì, xếp chashu lên trên rồi chan nước dùng nóng."]),
            ["pasta-carbonara"] = new(
                [("Mì spaghetti", 200m, "g"), ("Guanciale", 100m, "g"), ("Trứng", 2m, "quả")],
                ["Luộc mì vừa chín và áp chảo guanciale cho giòn.", "Trộn mì nóng với trứng và guanciale để tạo sốt sánh mịn."]),
            ["beef-steak-bo-toi"] = new(
                [("Thịt bò thăn", 250m, "g"), ("Bơ", 30m, "g"), ("Tỏi", 3m, "tép")],
                ["Áp chảo thịt bò đến độ chín mong muốn.", "Thêm bơ và tỏi, rưới bơ nóng lên thịt rồi để thịt nghỉ trước khi dùng."])
        };

    public static bool Ensure(Recipe recipe, ApplicationDbContext? context = null)
    {
        if (!Templates.TryGetValue(recipe.Slug, out var template))
            return false;

        var changed = false;
        if (recipe.Ingredients.Count == 0)
        {
            for (var i = 0; i < template.Ingredients.Length; i++)
            {
                var item = template.Ingredients[i];
                var ingredient = RecipeIngredient.Create(recipe.Id, item.Name, item.Quantity, item.Unit, null, i + 1);
                if (context is null)
                    recipe.Ingredients.Add(ingredient);
                else
                    context.RecipeIngredients.Add(ingredient);
            }
            changed = true;
        }

        if (recipe.Steps.Count == 0)
        {
            for (var i = 0; i < template.Steps.Length; i++)
            {
                var step = RecipeStep.Create(recipe.Id, i + 1, template.Steps[i]);
                if (context is null)
                    recipe.Steps.Add(step);
                else
                    context.RecipeSteps.Add(step);
            }
            if (string.IsNullOrWhiteSpace(recipe.Instructions))
                recipe.Instructions = string.Join(Environment.NewLine,
                    template.Steps.Select((step, index) => $"{index + 1}. {step}"));
            changed = true;
        }

        return changed;
    }
}
