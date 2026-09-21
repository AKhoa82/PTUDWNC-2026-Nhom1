using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CulinaryBlog.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Seed Categories (đảm bảo ít nhất 20 categories)
        var categoryList = await EnsureCategoriesAsync(context);

        // 2. Seed Recipes (đảm bảo ít nhất 100 recipes, mỗi recipe >= 10 ingredients và >= 5 steps)
        await EnsureRecipesAsync(context, categoryList);
    }

    private static async Task<List<Category>> EnsureCategoriesAsync(ApplicationDbContext context)
    {
        var existingCategories = await context.Categories.ToListAsync();
        var existingSlugs = new HashSet<string>(existingCategories.Select(c => c.Slug), StringComparer.OrdinalIgnoreCase);

        var predefinedCategories = new List<(string Name, string Slug, string Description)>
        {
            ("Món Việt", "mon-viet", "Các món ăn truyền thống đậm đà bản sắc Việt Nam"),
            ("Món Á", "mon-a", "Tinh hoa ẩm thực đặc sắc từ các nước Châu Á"),
            ("Món Âu", "mon-au", "Ẩm thực phong cách Châu Âu tinh tế và sang trọng"),
            ("Món Chay", "mon-chay", "Các món thanh tịnh, dinh dưỡng từ thực vật"),
            ("Món Khai Vị", "mon-khai-vi", "Những món mở đầu kích thích vị giác cho bữa tiệc"),
            ("Món Tráng Miệng", "mon-trang-mieng", "Chè, kem, trái cây và các món ngọt nhẹ sau bữa ăn"),
            ("Món Nướng & BBQ", "mon-nuong-bbq", "Các món nướng than hoa, sốt ướp đậm vị cho tiệc ngoài trời"),
            ("Món Canh & Súp", "mon-canh-sup", "Các món canh ấm bụng, thanh nhiệt và bổ dưỡng"),
            ("Hải Sản", "hai-san", "Hải sản tươi sống chế biến hấp dẫn: tôm, cua, mực, cá"),
            ("Món Ăn Sáng", "mon-an-sang", "Bữa sáng nhanh gọn, cung cấp đầy đủ năng lượng cho ngày mới"),
            ("Món Ý", "mon-y", "Pizza, Pasta và những nét ẩm thực lãng mạn nước Ý"),
            ("Món Hàn Quốc", "mon-han-quoc", "Kimchi, kimbap, tokbokki và các món cay nồng xứ kim chi"),
            ("Món Nhật Bản", "mon-nhat-ban", "Sushi, sashimi, ramen cầu kỳ chuẩn vị Nhật"),
            ("Món Thái Lan", "mon-thai-lan", "Hương vị chua cay mặn ngọt bùng nổ đặc trưng ẩm thực Thái"),
            ("Món Ăn Vặt", "mon-an-vat", "Những món ăn chơi hấp dẫn, dễ làm tại nhà"),
            ("Món Cuốn & Trộn", "mon-cuon-tron", "Gỏi cuốn, nộm thanh mát, ít dầu mỡ"),
            ("Món Hầm & Kho", "mon-ham-kho", "Thịt kho, cá kho, bò hầm đậm đà đưa cơm"),
            ("Món Xào & Chiên", "mon-xao-chien", "Các món chiên giòn, xào thơm lừng cho mâm cơm gia đình"),
            ("Bánh Ngọt & Bánh Mì", "banh-ngot-banh-mi", "Bánh mì giòn, bánh ngọt mềm xốp tự nướng tại nhà"),
            ("Đồ Uống & Pha Chế", "do-uong-pha-che", "Trà sữa, cocktail, sinh tố mát lạnh giải nhiệt"),
            ("Món Eat Clean & Healthy", "mon-eat-clean-healthy", "Thực đơn dinh dưỡng, ít calo, giữ dáng khỏe mạnh"),
            ("Món Lẩu", "mon-lau", "Các nồi lẩu nghi ngút khói sum vầy cùng gia đình"),
            ("Gia Vị & Nước Sốt", "gia-vi-nuoc-sot", "Công thức pha nước chấm, sốt ướp bất bại"),
            ("Ẩm Thực Đường Phố", "am-thuc-duong-pho", "Món ngon đường phố nức tiếng gần xa"),
            ("Món Ăn Gia Đình", "mon-an-gia-dinh", "Mâm cơm ấm cúng thường nhật mỗi ngày")
        };

        var toAdd = new List<Category>();
        foreach (var item in predefinedCategories)
        {
            if (!existingSlugs.Contains(item.Slug))
            {
                toAdd.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    Name = item.Name,
                    Slug = item.Slug,
                    Description = item.Description,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (toAdd.Count > 0)
        {
            await context.Categories.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
            existingCategories.AddRange(toAdd);
        }

        return existingCategories;
    }

    private static async Task EnsureRecipesAsync(ApplicationDbContext context, List<Category> categories)
    {
        var currentRecipeCount = await context.Recipes.CountAsync();
        if (currentRecipeCount >= 100)
        {
            return;
        }

        var needed = 100 - currentRecipeCount;
        var newRecipes = new List<Recipe>();

        var random = new Random(42); // Cố định seed để dữ liệu sinh nhất quán

        var recipeTemplates = GetRecipeTemplates();

        int templateIndex = 0;
        for (int i = 0; i < needed; i++)
        {
            var template = recipeTemplates[templateIndex % recipeTemplates.Count];
            templateIndex++;

            var category = categories[random.Next(categories.Count)];
            var recipeId = Guid.NewGuid();
            var title = i >= recipeTemplates.Count 
                ? $"{template.Title} Kiểu {i + 1}" 
                : template.Title;
            var slug = Slugify(title);

            // Sinh ít nhất 10 nguyên liệu
            var ingredients = GenerateIngredients(recipeId, random, template.BaseIngredients);

            // Sinh ít nhất 5 bước chế biến
            var steps = GenerateSteps(recipeId, random, template.BaseSteps);

            // Tạo Instructions dạng Markdown
            var instructionsMarkdown = BuildInstructionsMarkdown(ingredients, steps);

            var recipe = new Recipe
            {
                Id = recipeId,
                Title = title,
                Slug = slug,
                Description = template.Description,
                ImageUrl = template.ImageUrl,
                PrepTimeMinutes = random.Next(10, 45),
                CookingTimeMinutes = random.Next(15, 90),
                Servings = random.Next(2, 6),
                Difficulty = (RecipeDifficulty)random.Next(1, 4),
                Status = RecipeStatus.Published,
                Instructions = instructionsMarkdown,
                CategoryId = category.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 90)),
                PublishedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                Ingredients = ingredients,
                Steps = steps
            };

            newRecipes.Add(recipe);
        }

        await context.Recipes.AddRangeAsync(newRecipes);
        await context.SaveChangesAsync();
    }

    private static List<RecipeIngredient> GenerateIngredients(Guid recipeId, Random random, List<string>? baseNames)
    {
        var ingredientPool = new[]
        {
            ("Thịt bò thăn", "gram", 200, 500, "thái lát mỏng vừa ăn"),
            ("Thịt ba chỉ heo", "gram", 300, 600, "cắt miếng vuông vừa miệng"),
            ("Tôm sú tươi", "gram", 250, 500, "lột vỏ, bỏ chỉ lưng"),
            ("Thịt gà ta", "gram", 400, 800, "chặt miếng vừa ăn"),
            ("Mực nang tươi", "gram", 200, 400, "khía vảy rồng đẹp mắt"),
            ("Hành tím", "củ", 3, 6, "bóc vỏ băm nhuyễn"),
            ("Tỏi khô", "tép", 4, 8, "đập dập băm nhỏ"),
            ("Gừng tươi", "nhánh", 1, 2, "cạo vỏ thái sợi chỉ"),
            ("Hành lá", "nhánh", 2, 5, "cắt khúc 3cm"),
            ("Ngò rí (rau mùi)", "nhánh", 2, 4, "rửa sạch để ráo"),
            ("Nước mắm cá cơm", "thìa canh", 1, 3, "loại ngon 40 độ đạm"),
            ("Dầu hào", "thìa canh", 1, 2, "chất lượng cao"),
            ("Hạt nêm", "thìa cà phê", 1, 2, "nêm vừa khẩu vị"),
            ("Tiêu đen xay", "thìa cà phê", 1, 2, "tiêu Phú Quốc thơm nồng"),
            ("Đường tinh luyện", "thìa cà phê", 1, 3, "cân bằng vị giác"),
            ("Dầu ăn thực vật", "thìa canh", 2, 4, "dầu hoa cải hoặc đậu nành"),
            ("Ớt sừng đỏ", "quả", 1, 3, "bỏ hạt thái lát xéo"),
            ("Cà rốt tươi", "củ", 1, 2, "gọt vỏ tỉa hoa thái lát"),
            ("Nấm hương khô", "gram", 50, 100, "ngâm nở rửa sạch"),
            ("Cà chua chín", "quả", 2, 4, "bổ múi cau"),
            ("Dầu mè thơm", "thìa cà phê", 1, 2, "thêm vào cuối cho thơm"),
            ("Rượu trắng nấu ăn", "thìa canh", 1, 2, "khử mùi tanh thịt cá"),
            ("Bột bắp", "thìa canh", 1, 2, "hòa tan cùng 3 thìa nước lọc")
        };

        var ingredients = new List<RecipeIngredient>();
        var selectedIndexes = new HashSet<int>();

        // Đảm bảo ít nhất 10 nguyên liệu
        int totalIngredients = random.Next(10, 13);

        while (selectedIndexes.Count < totalIngredients)
        {
            selectedIndexes.Add(random.Next(ingredientPool.Length));
        }

        int order = 1;
        foreach (var idx in selectedIndexes)
        {
            var item = ingredientPool[idx];
            decimal quantity = random.Next(item.Item3, item.Item4 + 1);

            ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Name = item.Item1,
                Quantity = quantity,
                Unit = item.Item2,
                Notes = item.Item5,
                OrderIndex = order++
            });
        }

        return ingredients;
    }

    private static List<RecipeStep> GenerateSteps(Guid recipeId, Random random, List<(string Title, string Desc)>? baseSteps)
    {
        var standardSteps = new[]
        {
            ("Sơ chế nguyên liệu tươi sống", "Rửa sạch toàn bộ các loại thịt, cá, rau củ với nước muối loãng. Để ráo nước hoàn toàn rồi thái cắt theo kích thước vừa ăn.", 10),
            ("Ướp gia vị ngấm đều", "Cho nguyên liệu chính vào âu lớn, ướp cùng nước mắm, tiêu, hạt nêm, hành tỏi băm. Đảo đều và để yên trong 15-20 phút cho thấm vị.", 20),
            ("Chuẩn bị chảo và phi thơm gia vị", "Đặt chảo hoặc nồi lên bếp lửa vừa, cho 2 thìa dầu ăn đun nóng rồi thả hành tỏi, gừng băm vào phi vàng thơm lừng.", 5),
            ("Chế biến và đảo đều nguyên liệu", "Trút phần nguyên liệu đã ướp vào đảo nhanh tay ở lửa lớn để săn bề mặt và giữ được vị ngọt tự nhiên, sau đó hạ lửa vừa nấu chín tới.", 15),
            ("Nêm nếm và hoàn thiện hương vị", "Cho tiếp rau củ đi kèm vào đảo chín tới, nêm nếm lại gia vị cho vừa miệng, thêm chút tiêu đen xay và hành ngò cắt khúc.", 5),
            ("Trình bày và thưởng thức", "Tắt bếp, múc món ăn ra đĩa sâu lòng hoặc tô lớn, rắc ngò rí và tiêu lên trên. Dùng nóng cùng cơm trắng hoặc bánh mì sẽ ngon nhất.", 5)
        };

        var steps = new List<RecipeStep>();
        int stepCount = random.Next(5, 7); // Từ 5 đến 6 bước

        for (int i = 0; i < stepCount; i++)
        {
            var stepTemplate = standardSteps[i % standardSteps.Length];
            steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                StepNumber = i + 1,
                Title = stepTemplate.Item1,
                Description = stepTemplate.Item2,
                TimerMinutes = stepTemplate.Item3,
                ImageUrl = null
            });
        }

        return steps;
    }

    private static string BuildInstructionsMarkdown(List<RecipeIngredient> ingredients, List<RecipeStep> steps)
    {
        var sb = new StringBuilder();

        sb.AppendLine("### 📋 Danh sách nguyên liệu cần chuẩn bị:");
        foreach (var ing in ingredients)
        {
            var notes = string.IsNullOrWhiteSpace(ing.Notes) ? "" : $" ({ing.Notes})";
            sb.AppendLine($"- **{ing.Name}**: {ing.Quantity:0.#} {ing.Unit}{notes}");
        }

        sb.AppendLine();
        sb.AppendLine("### 🍳 Các bước thực hiện chi tiết:");
        foreach (var step in steps)
        {
            var timer = step.TimerMinutes.HasValue ? $" *(Thời gian: ~{step.TimerMinutes} phút)*" : "";
            sb.AppendLine($"#### Bước {step.StepNumber}: {step.Title}{timer}");
            sb.AppendLine(step.Description);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Guid.NewGuid().ToString("n")[..8];

        string unaccented = RemoveVietnameseSigns(text.ToLowerInvariant());
        var sb = new StringBuilder();
        foreach (char c in unaccented)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == ' ' || c == '-')
            {
                if (sb.Length > 0 && sb[^1] != '-')
                    sb.Append('-');
            }
        }

        var result = sb.ToString().Trim('-');
        return $"{result}-{Guid.NewGuid().ToString("n")[..6]}";
    }

    private static string RemoveVietnameseSigns(string text)
    {
        string[] arr1 = ["á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ"];
        string[] arr2 = ["é", "è", "ẻ", "ẽ", "ẹ", "ê", "ế", "ề", "ể", "ễ", "ệ"];
        string[] arr3 = ["í", "ì", "ỉ", "ĩ", "ị"];
        string[] arr4 = ["ó", "ò", "ỏ", "õ", "ọ", "ô", "ố", "ồ", "ổ", "ỗ", "ộ", "ơ", "ớ", "ờ", "ở", "ỡ", "ợ"];
        string[] arr5 = ["ú", "ù", "ủ", "ũ", "ụ", "ư", "ứ", "ừ", "ử", "ữ", "ự"];
        string[] arr6 = ["ý", "ỳ", "ỷ", "ỹ", "ỵ"];
        string[] arr7 = ["đ"];

        foreach (var s in arr1) text = text.Replace(s, "a");
        foreach (var s in arr2) text = text.Replace(s, "e");
        foreach (var s in arr3) text = text.Replace(s, "i");
        foreach (var s in arr4) text = text.Replace(s, "o");
        foreach (var s in arr5) text = text.Replace(s, "u");
        foreach (var s in arr6) text = text.Replace(s, "y");
        foreach (var s in arr7) text = text.Replace(s, "d");

        return text;
    }

    private static List<RecipeTemplate> GetRecipeTemplates()
    {
        return new List<RecipeTemplate>
        {
            new("Phở Bò Tái Lăn Hà Nội", "Món phở truyền thống với thịt bò thăn xào lăn thơm phức mùi tỏi và nước dùng ngọt xương thanh tao.", "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=800"),
            new("Bún Bò Huế Cay Nồng", "Hương vị cay nồng đặc trưng đất cố đô với mắm ruốc, sả ớt, bắp bò giòn mềm và chả cua thơm ngon.", "https://images.unsplash.com/photo-1569050467447-ce54b3bbc37d?w=800"),
            new("Cơm Tấm Sườn Bì Chả Sài Gòn", "Đĩa cơm tấm thơm mùi gạo tấm, sườn nướng mỡ hành đậm vị, chả trứng béo bùi và bì giòn rụm.", "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800"),
            new("Gỏi Cuốn Tôm Thịt Thanh Mát", "Món cuốn tươi mát chấm cùng tương đen bơ đậu phộng béo bùi, giàu dinh dưỡng cho ngày hè.", "https://images.unsplash.com/photo-1534422298391-e4f8c172dddb?w=800"),
            new("Cơm Chiên Dương Châu Hải Sản", "Hạt cơm vàng óng tơi xốp, quyện cùng tôm tươi, lạp xưởng, đậu Hà Lan và trứng béo thơm.", "https://images.unsplash.com/photo-1603133872878-684f208fb84b?w=800"),
            new("Mì Ramen Tonkotsu Nhật Bản", "Nước dùng xương hầm 12 tiếng sánh đặc béo ngậy, sợi mì tươi dai dai kèm thịt chashu mềm tan.", "https://images.unsplash.com/photo-1569718212165-3a8278d5f624?w=800"),
            new("Bò Kho Tiêu Bánh Mì Giòn", "Thịt bò nạm gân hầm mềm rục cùng cà rốt, nước sốt sánh sệt dậy mùi sả ớt và hoa hồi.", "https://images.unsplash.com/photo-1547496502-affa22d38842?w=800"),
            new("Pasta Ý Sốt Kem Carbonara", "Mì Ý spaghetti quyện sốt trứng phô mai Parmesan béo ngậy và thịt ba chỉ xông khói áp chảo giòn.", "https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9?w=800"),
            new("Beef Steak Bơ Tỏi Thảo Mộc", "Bít tết thăn lưng bò áp chảo bơ tỏi hương thảo mộc chín tái chuẩn vị Âu.", "https://images.unsplash.com/photo-1546833999-b9f581a1996d?w=800"),
            new("Canh Chua Cá Hú Miền Tây", "Nước canh chua ngọt thanh vị me, thịt cá hú béo ngậy nấu cùng bạc hà, cà chua, thơm và đậu bắp.", "https://images.unsplash.com/photo-1541832676-9b763b0239ab?w=800"),
            new("Thịt Heo Kho Tàu Trứng Vịt", "Món ăn truyền thống ngày Tết với thịt ba rọi mềm rục béo thơm, nước dừa ngấm đậm đà.", "https://images.unsplash.com/photo-1563379091339-03b21ab4a4f8?w=800"),
            new("Lẩu Thái Tom Yum Hải Sản", "Nước lẩu chua cay bùng nổ hương lá chanh chúc, sả, gừng riềng cùng tôm, mực tươi roi rói.", "https://images.unsplash.com/photo-1555126634-323283e090fa?w=800"),
            new("Gà Hấp Lá Chanh Muối Tiêu", "Gà ta thả vườn hấp da vàng giòn sần sật, thịt ngọt tự nhiên chấm cùng muối tiêu chanh ớt.", "https://images.unsplash.com/photo-1598515214211-89d3c73ae83b?w=800"),
            new("Bánh Xèo Giòn Rụm Tôm Nhảy", "Vỏ bánh xèo giòn rụm vàng ươm nghệ tươi, ngập tràn nhân tôm thịt giá đỗ cuốn rau rừng bánh tráng.", "https://images.unsplash.com/photo-1565299585323-38d6b0865b47?w=800"),
            new("Salad Ức Gà Sốt Mè Rang Healthy", "Ức gà áp chảo mềm ngọt kết hợp xà lách giòn tươi, cà chua bi và sốt mè rang thanh đạm.", "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?w=800"),
            new("Bánh Mì Nướng Bơ Tỏi Phô Mai", "Bánh mì giòn rụm thơm lừng sốt bơ tỏi ngập tràn phô mai kéo sợi thơm phức.", "https://images.unsplash.com/photo-1509722747041-616f39b57569?w=800"),
            new("Sườn Xào Chua Ngọt Bắc Bộ", "Sườn non chiên xém cạnh rim sốt giấm đường chua ngọt óng ả, màu sắc bắt mắt thơm lừng.", "https://images.unsplash.com/photo-1544025162-d76694265947?w=800"),
            new("Cá Chẽm Hấp Xì Dầu Gừng Sả", "Cá chẽm tươi sống hấp giữ trọn độ ngọt, ngấm vị xì dầu thanh tao thơm phức mùi gừng hành.", "https://images.unsplash.com/photo-1534939561126-855b8675edd7?w=800"),
            new("Mì Quảng Tôm Thịt Đậm Đà", "Sợi mì vàng mềm ăn kèm nước nhưn tôm thịt sánh đặc, bánh tráng nướng giòn và rau sống thơm ngát.", "https://images.unsplash.com/photo-1552611052-33e04de081de?w=800"),
            new("Trà Sữa Trân Châu Đường Đen", "Hương trà đen nồng nàn hòa quyện sữa béo bùi và trân châu thủ công mềm dẻo ngập sốt đường nâu.", "https://images.unsplash.com/photo-1558857563-b37cf5c8b093?w=800"),
            new("Bánh Flan Caramen Mềm Tan", "Bánh flan mềm mịn không rỗ, thơm nức mùi trứng sữa quyện vị đăng đắng ngọt dịu của caramen.", "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=800"),
            new("Bún Chả Hà Nội Nướng Than Hoa", "Chả viên và chả miếng nướng xém cạnh trên than hoa đượm khói, chấm nước mắm chua ngọt ấm nóng.", "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=800"),
            new("Cơm Niêu Cháy Giòn Kho Quẹt", "Cơm niêu đáy giòn rụm rôm rốp quết cùng tộ kho quẹt tóp mỡ tôm khô cay nồng hấp dẫn.", "https://images.unsplash.com/photo-1516684732162-798a0062be99?w=800"),
            new("Cháo Sườn Sụn Bách Thảo", "Bát cháo sườn xay mịn như nhung, sườn sụn sần sật béo ngậy kèm quẩy giòn và trứng bách thảo.", "https://images.unsplash.com/photo-1540420773420-3366772f4999?w=800"),
            new("Pizza Hải Sản Phô Mai Mozzarella", "Đế bánh pizza giòn xốp nướng lò củi phủ đầy tôm, mực, sốt cà chua và phô mai kéo sợi bất tận.", "https://images.unsplash.com/photo-1513104890138-7c749659a591?w=800")
        };
    }

    private record RecipeTemplate(
        string Title,
        string Description,
        string ImageUrl,
        List<string>? BaseIngredients = null,
        List<(string Title, string Desc)>? BaseSteps = null);
}
